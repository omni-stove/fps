using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    internal sealed class JsonParser
    {
        // 項目は 2 段までなので、それより深く入れ子にしたものは読む前に断る。
        private const int _defaultMaxDepth = 8;

        private const char _byteOrderMark = (char)0xFEFF;

        private readonly string _text;
        private readonly int _maxDepth;
        private int _position;
        private int _line = 1;
        private int _lineStart;

        public JsonParser(string text)
            : this(text, _defaultMaxDepth)
        {
        }

        public JsonParser(string text, int maxDepth)
        {
            if (maxDepth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDepth), maxDepth, "the nesting limit must be at least 1");
            }

            _text = text;
            _maxDepth = maxDepth;
        }

        public JsonNode ParseDocument()
        {
            if (_position < _text.Length && _text[_position] == _byteOrderMark)
            {
                _position++;
                _lineStart = _position;
            }

            SkipWhitespace();
            var value = ParseValue(0);
            SkipWhitespace();
            if (_position != _text.Length)
            {
                throw Error("unexpected text after the value");
            }

            return value;
        }

        private JsonNode ParseValue(int depth)
        {
            if (_position >= _text.Length)
            {
                throw Error("unexpected end of input");
            }

            var line = _line;
            var column = Column;
            var c = _text[_position];
            switch (c)
            {
                case '{':
                    return ParseObject(depth + 1, line, column);
                case '[':
                    return ParseArray(depth + 1, line, column);
                case '"':
                    return new JsonNode(JsonNodeKind.String, ParseString(), line, column);
                case 't':
                    ExpectLiteral("true");
                    return new JsonNode(JsonNodeKind.True, null, line, column);
                case 'f':
                    ExpectLiteral("false");
                    return new JsonNode(JsonNodeKind.False, null, line, column);
                case 'n':
                    ExpectLiteral("null");
                    return new JsonNode(JsonNodeKind.Null, null, line, column);
                default:
                    if (c == '-' || IsDigit(c))
                    {
                        return new JsonNode(JsonNodeKind.Number, ParseNumber(), line, column);
                    }

                    throw Error($"unexpected character '{c}'");
            }
        }

        private JsonNode ParseObject(int depth, int line, int column)
        {
            CheckDepth(depth);
            _position++;
            var node = new JsonNode(JsonNodeKind.Object, null, line, column);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            SkipWhitespace();
            if (TryConsume('}'))
            {
                return node;
            }

            while (true)
            {
                SkipWhitespace();
                if (_position >= _text.Length || _text[_position] != '"')
                {
                    throw Error("expected a string key");
                }

                var keyLine = _line;
                var keyColumn = Column;
                var key = ParseString();
                if (!keys.Add(key))
                {
                    throw new FormatException(
                        $"duplicate key \"{key}\" at line {keyLine.ToString(CultureInfo.InvariantCulture)}, "
                        + $"column {keyColumn.ToString(CultureInfo.InvariantCulture)}");
                }

                SkipWhitespace();
                Expect(':');
                SkipWhitespace();
                node.Members.Add(new KeyValuePair<string, JsonNode>(key, ParseValue(depth)));
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return node;
                }

                Expect(',');
            }
        }

        private JsonNode ParseArray(int depth, int line, int column)
        {
            CheckDepth(depth);
            _position++;
            var node = new JsonNode(JsonNodeKind.Array, null, line, column);
            SkipWhitespace();
            if (TryConsume(']'))
            {
                return node;
            }

            while (true)
            {
                SkipWhitespace();
                node.Elements.Add(ParseValue(depth));
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return node;
                }

                Expect(',');
            }
        }

        private string ParseString()
        {
            _position++;
            var builder = new StringBuilder();
            while (true)
            {
                if (_position >= _text.Length)
                {
                    throw Error("unterminated string");
                }

                var c = _text[_position];
                if (c == '"')
                {
                    _position++;
                    return builder.ToString();
                }

                if (c < ' ')
                {
                    throw Error("control character in string");
                }

                _position++;
                if (c != '\\')
                {
                    builder.Append(c);
                    continue;
                }

                if (_position >= _text.Length)
                {
                    throw Error("unterminated string");
                }

                var escape = _text[_position];
                switch (escape)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escape);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        _position++;
                        builder.Append(ParseHex4());
                        continue;
                    default:
                        throw Error($"invalid escape '\\{escape}'");
                }

                _position++;
            }
        }

        private char ParseHex4()
        {
            if (_position + 4 > _text.Length)
            {
                throw Error("incomplete unicode escape");
            }

            var value = 0;
            for (var i = 0; i < 4; i++)
            {
                var c = _text[_position];
                int digit;
                if (IsDigit(c))
                {
                    digit = c - '0';
                }
                else if (c >= 'a' && c <= 'f')
                {
                    digit = c - 'a' + 10;
                }
                else if (c >= 'A' && c <= 'F')
                {
                    digit = c - 'A' + 10;
                }
                else
                {
                    throw Error("invalid unicode escape");
                }

                value = (value * 16) + digit;
                _position++;
            }

            return (char)value;
        }

        private string ParseNumber()
        {
            var start = _position;
            TryConsume('-');
            if (_position >= _text.Length || !IsDigit(_text[_position]))
            {
                throw Error("expected a digit");
            }

            if (_text[_position] == '0')
            {
                _position++;
                if (_position < _text.Length && IsDigit(_text[_position]))
                {
                    throw Error("numbers must not have leading zeros");
                }
            }
            else
            {
                SkipDigits();
            }

            if (TryConsume('.'))
            {
                RequireDigits();
            }

            if (TryConsume('e') || TryConsume('E'))
            {
                if (!TryConsume('+'))
                {
                    TryConsume('-');
                }

                RequireDigits();
            }

            return _text.Substring(start, _position - start);
        }

        private void RequireDigits()
        {
            if (_position >= _text.Length || !IsDigit(_text[_position]))
            {
                throw Error("expected a digit");
            }

            SkipDigits();
        }

        private void SkipDigits()
        {
            while (_position < _text.Length && IsDigit(_text[_position]))
            {
                _position++;
            }
        }

        private void ExpectLiteral(string literal)
        {
            if (_position + literal.Length > _text.Length
                || string.CompareOrdinal(_text, _position, literal, 0, literal.Length) != 0)
            {
                throw Error("invalid literal");
            }

            _position += literal.Length;
        }

        private void Expect(char expected)
        {
            if (!TryConsume(expected))
            {
                throw Error($"expected '{expected}'");
            }
        }

        private bool TryConsume(char expected)
        {
            if (_position < _text.Length && _text[_position] == expected)
            {
                _position++;
                return true;
            }

            return false;
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length)
            {
                var c = _text[_position];
                if (c == '\n')
                {
                    _line++;
                    _lineStart = _position + 1;
                }
                else if (c != ' ' && c != '\t' && c != '\r')
                {
                    return;
                }

                _position++;
            }
        }

        private void CheckDepth(int depth)
        {
            if (depth > _maxDepth)
            {
                throw Error("nesting is too deep");
            }
        }

        private int Column => _position - _lineStart + 1;

        private FormatException Error(string message)
        {
            var at = _position >= _text.Length
                ? "end of input"
                : $"line {_line.ToString(CultureInfo.InvariantCulture)}, column {Column.ToString(CultureInfo.InvariantCulture)}";
            return new FormatException(message + " at " + at);
        }

        private static bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }
    }
}

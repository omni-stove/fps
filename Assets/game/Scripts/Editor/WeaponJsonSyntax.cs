using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Editor
{
    /// <summary>
    /// <see cref="WeaponJsonImporter"/> が読む JSON の文法だけを扱う。
    /// オブジェクトは <see cref="Dictionary{TKey,TValue}"/> (キーは string)、配列は <see cref="List{T}"/>、文字列は string、
    /// 数は <see cref="WeaponJsonNumber"/>、true/false は bool、null は null で返す。
    /// </summary>
    public static class WeaponJsonSyntax
    {
        private const int _maxDepth = 32;
        private const char _byteOrderMark = (char)0xFEFF;

        /// <summary>text 全体を 1 つの JSON 値として読む。</summary>
        /// <exception cref="FormatException">文法に合わない・同じキーが 2 度ある。メッセージに行と列を含む。</exception>
        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new Parser(text).ParseDocument();
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _position;

            public Parser(string text)
            {
                _text = text;
            }

            public object ParseDocument()
            {
                if (_position < _text.Length && _text[_position] == _byteOrderMark)
                {
                    _position++;
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

            private object ParseValue(int depth)
            {
                if (_position >= _text.Length)
                {
                    throw Error("unexpected end of input");
                }

                var c = _text[_position];
                switch (c)
                {
                    case '{':
                        return ParseObject(depth + 1);
                    case '[':
                        return ParseArray(depth + 1);
                    case '"':
                        return ParseString();
                    case 't':
                        ExpectLiteral("true");
                        return true;
                    case 'f':
                        ExpectLiteral("false");
                        return false;
                    case 'n':
                        ExpectLiteral("null");
                        return null;
                    default:
                        if (c == '-' || IsDigit(c))
                        {
                            return ParseNumber();
                        }

                        throw Error($"unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ParseObject(int depth)
            {
                CheckDepth(depth);
                _position++;
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (_position >= _text.Length || _text[_position] != '"')
                    {
                        throw Error("expected a string key");
                    }

                    var keyStart = _position;
                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    var value = ParseValue(depth);
                    if (!result.TryAdd(key, value))
                    {
                        _position = keyStart;
                        throw Error($"duplicate key \"{key}\"");
                    }

                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return result;
                    }

                    Expect(',');
                }
            }

            private List<object> ParseArray(int depth)
            {
                CheckDepth(depth);
                _position++;
                var result = new List<object>();
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    result.Add(ParseValue(depth));
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return result;
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

                    var c = _text[_position++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c < ' ')
                    {
                        _position--;
                        throw Error("control character in string");
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (_position >= _text.Length)
                    {
                        throw Error("unterminated string");
                    }

                    var escape = _text[_position++];
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
                            if (_position + 4 > _text.Length
                                || !int.TryParse(
                                    _text.Substring(_position, 4),
                                    NumberStyles.AllowHexSpecifier,
                                    CultureInfo.InvariantCulture,
                                    out var code))
                            {
                                throw Error("invalid \\u escape");
                            }

                            builder.Append((char)code);
                            _position += 4;
                            break;
                        default:
                            _position--;
                            throw Error($"invalid escape '\\{escape}'");
                    }
                }
            }

            // 値の型 (int か float か) は項目ごとに決まるので、ここでは JSON の数の文法だけを確かめて元の文字列のまま返す
            private WeaponJsonNumber ParseNumber()
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

                var isInteger = true;
                if (TryConsume('.'))
                {
                    isInteger = false;
                    RequireDigits();
                }

                if (_position < _text.Length && (_text[_position] == 'e' || _text[_position] == 'E'))
                {
                    isInteger = false;
                    _position++;
                    if (!TryConsume('+'))
                    {
                        TryConsume('-');
                    }

                    RequireDigits();
                }

                return new WeaponJsonNumber(_text.Substring(start, _position - start), isInteger);
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
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
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

            private FormatException Error(string message)
            {
                var line = 1;
                var column = 1;
                for (var i = 0; i < _position && i < _text.Length; i++)
                {
                    if (_text[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }

                return new FormatException(
                    $"{message} at line {line.ToString(CultureInfo.InvariantCulture)}, column {column.ToString(CultureInfo.InvariantCulture)}");
            }

            private static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }
        }
    }

    /// <summary>JSON の数。文法は確かめてあり、<see cref="Text"/> は書かれたままの文字列。</summary>
    public sealed class WeaponJsonNumber
    {
        public WeaponJsonNumber(string text, bool isInteger)
        {
            Text = text ?? throw new ArgumentNullException(nameof(text));
            IsInteger = isInteger;
        }

        public string Text { get; }

        /// <summary>小数点も指数も無い。</summary>
        public bool IsInteger { get; }

        public override string ToString()
        {
            return Text;
        }
    }
}

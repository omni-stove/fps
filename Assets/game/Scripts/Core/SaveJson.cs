using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// セーブデータ用の小さな JSON の読み書き。外部パッケージに頼らず Core だけで使う。
    /// 読み込みはオブジェクト・配列・文字列・整数・true/false/null を扱い、小数と指数は受け付けない。
    /// 読んだ値は <see cref="Dictionary{TKey,TValue}"/> (キーは string)、<see cref="List{T}"/>、string、long、bool、null で返す。
    /// </summary>
    internal static class SaveJson
    {
        /// <summary>入れ子の深さの上限。壊れたデータで再帰が深くなりすぎないようにする。</summary>
        private const int _maxDepth = 32;

        /// <summary>text 全体を 1 つの JSON 値として読む。文法に合わなければ <see cref="FormatException"/>。</summary>
        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var parser = new Parser(text);
            return parser.ParseDocument();
        }

        /// <summary>value を JSON の文字列リテラルとして書き足す。</summary>
        public static void AppendString(StringBuilder builder, string value)
        {
            builder.Append('"');
            foreach (var c in value)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        if (c < ' ')
                        {
                            builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(c);
                        }

                        break;
                }
            }

            builder.Append('"');
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
                SkipWhitespace();
                var value = ParseValue(0);
                SkipWhitespace();
                if (_position != _text.Length)
                {
                    throw Error("unexpected characters after the value");
                }

                return value;
            }

            private object ParseValue(int depth)
            {
                if (_position >= _text.Length)
                {
                    throw Error("unexpected end of data");
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
                            return ParseInteger();
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

                    var key = ParseString();
                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    var value = ParseValue(depth);
                    if (!result.TryAdd(key, value))
                    {
                        throw Error($"duplicate key '{key}'");
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
                            builder.Append(ParseHex4());
                            break;
                        default:
                            throw Error($"invalid escape '\\{escape}'");
                    }
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
                    var c = _text[_position++];
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
                }

                return (char)value;
            }

            /// <summary>JSON の数の文法のうち整数だけを読む。long に収まらなければ例外にする。</summary>
            private long ParseInteger()
            {
                var negative = TryConsume('-');
                if (_position >= _text.Length || !IsDigit(_text[_position]))
                {
                    throw Error("expected a digit");
                }

                var leadingZero = _text[_position] == '0';
                // 負の側で貯めると long.MinValue まで扱える。
                long value = 0;
                var digits = 0;
                while (_position < _text.Length && IsDigit(_text[_position]))
                {
                    var digit = _text[_position] - '0';
                    if (value < (long.MinValue + digit) / 10)
                    {
                        throw Error("number is out of range");
                    }

                    value = (value * 10) - digit;
                    digits++;
                    _position++;
                }

                if (leadingZero && digits > 1)
                {
                    throw Error("numbers must not have leading zeros");
                }

                if (_position < _text.Length && (_text[_position] == '.' || _text[_position] == 'e' || _text[_position] == 'E'))
                {
                    throw Error("only integers are supported");
                }

                if (negative)
                {
                    return value;
                }

                if (value == long.MinValue)
                {
                    throw Error("number is out of range");
                }

                return -value;
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
                return new FormatException(
                    $"invalid save data at position {_position.ToString(CultureInfo.InvariantCulture)}: {message}");
            }

            private static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }
        }
    }
}

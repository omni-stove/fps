using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="TextTable.FromJson"/> の読み取り。<c>{"key": "書式"}</c> の平たいオブジェクトだけを読む。
    /// Fps.Core は Unity でも素の .NET Standard 2.1 でも同じに動かすので、System.Text.Json に頼らずここで読む。
    /// </summary>
    internal static class TextTableJson
    {
        public static List<KeyValuePair<string, string>> Parse(string json)
        {
            var reader = new Reader(json);
            var entries = new List<KeyValuePair<string, string>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            reader.SkipBom();
            reader.SkipWhitespace();
            reader.Expect('{');
            reader.SkipWhitespace();

            if (reader.Peek() == '}')
            {
                reader.Advance();
            }
            else
            {
                while (true)
                {
                    reader.SkipWhitespace();
                    var keyAt = reader.Position;
                    var key = reader.ReadString();
                    if (key.Length == 0)
                    {
                        throw reader.Error("key must not be empty", keyAt);
                    }

                    if (!seen.Add(key))
                    {
                        throw reader.Error("key '" + key + "' appears more than once", keyAt);
                    }

                    reader.SkipWhitespace();
                    reader.Expect(':');
                    reader.SkipWhitespace();
                    if (reader.Peek() != '"')
                    {
                        throw reader.Error("value for key '" + key + "' must be a string", reader.Position);
                    }

                    entries.Add(new KeyValuePair<string, string>(key, reader.ReadString()));

                    reader.SkipWhitespace();
                    var separator = reader.Peek();
                    if (separator == ',')
                    {
                        reader.Advance();
                        continue;
                    }

                    if (separator == '}')
                    {
                        reader.Advance();
                        break;
                    }

                    throw reader.Error("expected ',' or '}'", reader.Position);
                }
            }

            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw reader.Error("unexpected text after the object", reader.Position);
            }

            return entries;
        }

        private sealed class Reader
        {
            private const int End = -1;

            private readonly string _json;
            private int _position;

            public Reader(string json)
            {
                _json = json;
            }

            public int Position => _position;

            public bool AtEnd => _position >= _json.Length;

            public int Peek()
            {
                return AtEnd ? End : _json[_position];
            }

            public void Advance()
            {
                _position++;
            }

            public void SkipBom()
            {
                if (Peek() == '﻿')
                {
                    _position++;
                }
            }

            public void SkipWhitespace()
            {
                while (!AtEnd)
                {
                    var c = _json[_position];
                    if (c != ' ' && c != '\t' && c != '\n' && c != '\r')
                    {
                        return;
                    }

                    _position++;
                }
            }

            public void Expect(char expected)
            {
                if (Peek() != expected)
                {
                    throw Error("expected '" + expected + "'", _position);
                }

                _position++;
            }

            public string ReadString()
            {
                var start = _position;
                Expect('"');
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("string is not closed", start);
                    }

                    var c = _json[_position++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c < ' ')
                    {
                        throw Error("control character must be escaped inside a string", _position - 1);
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("escape is not finished", _position - 1);
                    }

                    var escape = _json[_position++];
                    switch (escape)
                    {
                        case '"':
                            builder.Append('"');
                            break;
                        case '\\':
                            builder.Append('\\');
                            break;
                        case '/':
                            builder.Append('/');
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
                            builder.Append(ReadHex4());
                            break;
                        default:
                            throw Error("unknown escape '\\" + escape + "'", _position - 2);
                    }
                }
            }

            // サロゲートペアは 😀 の 2 つの \u がそれぞれ半分ずつを書くので、1 つずつ char として足せばつながる
            private char ReadHex4()
            {
                var start = _position - 2;
                if (_position + 4 > _json.Length)
                {
                    throw Error("\\u needs 4 hex digits", start);
                }

                var hex = _json.Substring(_position, 4);
                if (!ushort.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var code))
                {
                    throw Error("\\u needs 4 hex digits", start);
                }

                _position += 4;
                return (char)code;
            }

            public FormatException Error(string message, int at)
            {
                var line = 1;
                var column = 1;
                for (var i = 0; i < at && i < _json.Length; i++)
                {
                    if (_json[i] == '\n')
                    {
                        line++;
                        column = 1;
                    }
                    else
                    {
                        column++;
                    }
                }

                return new FormatException("text table JSON: " + message + " at line " + line + ", column " + column);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// 値がすべて文字列の、入れ子の無い JSON オブジェクト (<c>{"KEY": "value", ...}</c>) を読み書きする。
    /// 読むのは設定ファイル <c>eos.local.json</c>、書くのはサーバーのイベントのログ (<see cref="ServerEventLog"/>) のためのもので、
    /// Unity にも外部ライブラリにも依存しない。
    /// </summary>
    public static class FlatJsonObject
    {
        /// <summary>
        /// キーと値を、渡した順に空白も改行も入れない 1 行の JSON オブジェクトにする。
        /// 制御文字・<c>"</c>・<c>\</c>・行区切り (U+2028 / U+2029)・対になっていないサロゲートはエスケープするので、
        /// どんな文字列を渡しても結果は 1 行で、<see cref="TryParse"/> で同じキーと値に読み戻せる。
        /// </summary>
        /// <exception cref="ArgumentNullException">values か、そのキー・値のどれかが null。</exception>
        /// <exception cref="ArgumentException">同じキーが 2 度ある。</exception>
        public static string Format(IReadOnlyList<KeyValuePair<string, string>> values)
        {
            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            var keys = new HashSet<string>(StringComparer.Ordinal);
            var builder = new StringBuilder();
            builder.Append('{');
            for (var i = 0; i < values.Count; i++)
            {
                var pair = values[i];
                if (pair.Key == null)
                {
                    throw new ArgumentNullException(nameof(values), "key at index " + i.ToString(CultureInfo.InvariantCulture) + " is null");
                }

                if (pair.Value == null)
                {
                    throw new ArgumentNullException(nameof(values), "value of \"" + pair.Key + "\" is null");
                }

                if (!keys.Add(pair.Key))
                {
                    throw new ArgumentException("duplicate key \"" + pair.Key + "\"", nameof(values));
                }

                if (i > 0)
                {
                    builder.Append(',');
                }

                AppendString(builder, pair.Key);
                builder.Append(':');
                AppendString(builder, pair.Value);
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, string text)
        {
            builder.Append('"');
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        continue;
                    case '\\':
                        builder.Append("\\\\");
                        continue;
                    case '\n':
                        builder.Append("\\n");
                        continue;
                    case '\r':
                        builder.Append("\\r");
                        continue;
                    case '\t':
                        builder.Append("\\t");
                        continue;
                }

                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    builder.Append(c).Append(text[++i]);
                    continue;
                }

                // 行区切りは JSON としては正しいが、行ごとに読むログの集め方では改行として扱われることがある
                if (c < ' ' || c == ' ' || c == ' ' || char.IsSurrogate(c))
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    continue;
                }

                builder.Append(c);
            }

            builder.Append('"');
        }

        /// <param name="text">JSON の文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="values">成功したときのキーと値。失敗したときは null。</param>
        /// <param name="error">失敗したときの理由 (何文字目か付き)。成功したときは null。</param>
        /// <returns>文字列だけを値に持つ 1 つのオブジェクトとして読めたら true。</returns>
        public static bool TryParse(string text, out IReadOnlyDictionary<string, string> values, out string error)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            values = null;
            var reader = new Reader(text);
            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            if (reader.Peek() == '﻿')
            {
                reader.Advance();
            }

            reader.SkipWhitespace();
            if (!reader.Expect('{', out error))
            {
                return false;
            }

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
                    var keyPosition = reader.Position;
                    if (!reader.ReadString(out var key, out error))
                    {
                        return false;
                    }

                    reader.SkipWhitespace();
                    if (!reader.Expect(':', out error))
                    {
                        return false;
                    }

                    reader.SkipWhitespace();
                    if (reader.Peek() != '"')
                    {
                        error = reader.Describe("only string values are supported for \"" + key + "\"");
                        return false;
                    }

                    if (!reader.ReadString(out var value, out error))
                    {
                        return false;
                    }

                    if (result.ContainsKey(key))
                    {
                        error = "duplicate key \"" + key + "\" at " + (keyPosition + 1).ToString(CultureInfo.InvariantCulture);
                        return false;
                    }

                    result.Add(key, value);
                    reader.SkipWhitespace();
                    if (reader.Peek() == ',')
                    {
                        reader.Advance();
                        continue;
                    }

                    if (!reader.Expect('}', out error))
                    {
                        return false;
                    }

                    break;
                }
            }

            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                error = reader.Describe("unexpected text after the object");
                return false;
            }

            values = result;
            error = null;
            return true;
        }

        private sealed class Reader
        {
            private const char _end = '\0';

            private readonly string _text;

            public Reader(string text)
            {
                _text = text;
            }

            public int Position { get; private set; }

            public bool AtEnd => Position >= _text.Length;

            public char Peek()
            {
                return AtEnd ? _end : _text[Position];
            }

            public void Advance()
            {
                Position++;
            }

            public void SkipWhitespace()
            {
                while (!AtEnd && (Peek() == ' ' || Peek() == '\t' || Peek() == '\n' || Peek() == '\r'))
                {
                    Position++;
                }
            }

            public bool Expect(char expected, out string error)
            {
                if (AtEnd || Peek() != expected)
                {
                    error = Describe("expected '" + expected + "'");
                    return false;
                }

                Position++;
                error = null;
                return true;
            }

            public bool ReadString(out string value, out string error)
            {
                value = null;
                if (!Expect('"', out error))
                {
                    return false;
                }

                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        error = Describe("unterminated string");
                        return false;
                    }

                    var c = _text[Position++];
                    if (c == '"')
                    {
                        break;
                    }

                    if (c < ' ')
                    {
                        Position--;
                        error = Describe("control character in string");
                        return false;
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        error = Describe("unterminated escape");
                        return false;
                    }

                    var escaped = _text[Position++];
                    switch (escaped)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            builder.Append(escaped);
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
                            if (Position + 4 > _text.Length
                                || !int.TryParse(
                                    _text.AsSpan(Position, 4),
                                    NumberStyles.AllowHexSpecifier,
                                    CultureInfo.InvariantCulture,
                                    out var code))
                            {
                                error = Describe("invalid \\u escape");
                                return false;
                            }

                            builder.Append((char)code);
                            Position += 4;
                            break;
                        default:
                            Position--;
                            error = Describe("invalid escape '\\" + escaped + "'");
                            return false;
                    }
                }

                value = builder.ToString();
                error = null;
                return true;
            }

            public string Describe(string problem)
            {
                var at = AtEnd ? "end of input" : "position " + (Position + 1).ToString(CultureInfo.InvariantCulture);
                return problem + " at " + at;
            }
        }
    }
}

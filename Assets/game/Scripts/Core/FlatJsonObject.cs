using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // Unity にも外部ライブラリにも依存しないよう、入れ子の無い文字列だけの JSON オブジェクトを自前で書き、読みは共通の JsonParser に任せる。
    public static class FlatJsonObject
    {
        // 文字リテラルに U+2028 / U+2029 や BOM を直接書くと、C# が改行として扱ったり不可視で読めなくなったりするため、数値で書く
        private const char _lineSeparator = (char)0x2028;
        private const char _paragraphSeparator = (char)0x2029;

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
                if (c < ' ' || c == _lineSeparator || c == _paragraphSeparator || char.IsSurrogate(c))
                {
                    builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    continue;
                }

                builder.Append(c);
            }

            builder.Append('"');
        }

        public static bool TryParse(string text, out IReadOnlyDictionary<string, string> values, out string error)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            values = null;
            JsonNode root;
            try
            {
                root = new JsonParser(text).ParseDocument();
            }
            catch (FormatException exception)
            {
                error = exception.Message;
                return false;
            }

            if (root.Kind != JsonNodeKind.Object)
            {
                error = "expected '{' at " + root.Position;
                return false;
            }

            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var member in root.Members)
            {
                if (member.Value.Kind != JsonNodeKind.String)
                {
                    error = "only string values are supported for \"" + member.Key + "\" at " + member.Value.Position;
                    return false;
                }

                result.Add(member.Key, member.Value.Text);
            }

            values = result;
            error = null;
            return true;
        }
    }
}

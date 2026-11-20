using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 外部パッケージに頼らず Core だけで使えるよう、セーブデータに要る分だけの JSON を自前で読み書きする。
    internal static class SaveJson
    {
        // 壊れたデータで再帰が深くなりすぎないよう、入れ子の深さに上限を設ける。
        private const int _maxDepth = 32;

        private const char _byteOrderMark = (char)0xFEFF;

        private static readonly char[] _nonIntegerMarks = { '.', 'e', 'E' };

        public static object Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            // JsonParser は先頭の BOM を読み飛ばすが、セーブは BOM 付きを壊れたものとして扱ってきたので、それを変えない。
            if (text.Length > 0 && text[0] == _byteOrderMark)
            {
                throw Error("unexpected character at the start of the data");
            }

            JsonNode root;
            try
            {
                root = new JsonParser(text, _maxDepth).ParseDocument();
            }
            catch (FormatException e)
            {
                throw Error(e.Message, e);
            }

            return ToValue(root);
        }

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

        private static object ToValue(JsonNode node)
        {
            switch (node.Kind)
            {
                case JsonNodeKind.Object:
                    var map = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var member in node.Members)
                    {
                        map.Add(member.Key, ToValue(member.Value));
                    }

                    return map;
                case JsonNodeKind.Array:
                    var list = new List<object>(node.Elements.Count);
                    foreach (var element in node.Elements)
                    {
                        list.Add(ToValue(element));
                    }

                    return list;
                case JsonNodeKind.String:
                    return node.Text;
                case JsonNodeKind.Number:
                    return ToInteger(node);
                case JsonNodeKind.True:
                    return true;
                case JsonNodeKind.False:
                    return false;
                default:
                    return null;
            }
        }

        private static long ToInteger(JsonNode node)
        {
            if (node.Text.IndexOfAny(_nonIntegerMarks) >= 0)
            {
                throw Error("only integers are supported at " + node.Position);
            }

            if (!long.TryParse(node.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
            {
                throw Error("number is out of range at " + node.Position);
            }

            return value;
        }

        private static FormatException Error(string message, Exception inner = null)
        {
            return new FormatException("invalid save data: " + message, inner);
        }
    }
}

using System.Collections.Generic;
using System.Globalization;

namespace Shooter.Core
{
    internal enum JsonNodeKind
    {
        Object,
        Array,
        String,
        Number,
        True,
        False,
        Null,
    }

    internal sealed class JsonNode
    {
        public JsonNode(JsonNodeKind kind, string text, int line, int column)
        {
            Kind = kind;
            Text = text;
            Line = line;
            Column = column;
        }

        public JsonNodeKind Kind { get; }

        // 数を字句のまま持つのは、読む側の型 (float / int) で解釈し直すため。
        public string Text { get; }

        public int Line { get; }

        public int Column { get; }

        public List<KeyValuePair<string, JsonNode>> Members { get; } = new List<KeyValuePair<string, JsonNode>>();

        public List<JsonNode> Elements { get; } = new List<JsonNode>();

        public string Position => $"line {Line.ToString(CultureInfo.InvariantCulture)}, column {Column.ToString(CultureInfo.InvariantCulture)}";

        public string Describe()
        {
            switch (Kind)
            {
                case JsonNodeKind.Object:
                    return "an object";
                case JsonNodeKind.Array:
                    return "an array";
                case JsonNodeKind.String:
                    return $"the string \"{Text}\"";
                case JsonNodeKind.Number:
                    return $"the number {Text}";
                case JsonNodeKind.True:
                    return "true";
                case JsonNodeKind.False:
                    return "false";
                default:
                    return "null";
            }
        }
    }
}

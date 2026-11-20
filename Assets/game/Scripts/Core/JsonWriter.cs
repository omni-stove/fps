using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    internal sealed class JsonWriter
    {
        private readonly StringBuilder _builder = new StringBuilder("{");
        private int _depth = 1;
        private bool _first = true;

        public void Float(string name, float value)
        {
            Raw(name, FloatText(value));
        }

        public void Double(string name, double value)
        {
            Raw(name, DoubleText(value));
        }

        public void Int(string name, int value)
        {
            Raw(name, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Bool(string name, bool value)
        {
            Raw(name, value ? "true" : "false");
        }

        public void String(string name, string value)
        {
            Raw(name, Quote(value));
        }

        public void Enum<T>(string name, T value)
            where T : struct, System.Enum
        {
            Raw(name, "\"" + value.ToString() + "\"");
        }

        public void EnumArray<T>(string name, IEnumerable<T> values)
            where T : struct, System.Enum
        {
            var parts = new List<string>();
            foreach (var value in values)
            {
                parts.Add("\"" + value.ToString() + "\"");
            }

            Raw(name, "[" + string.Join(", ", parts) + "]");
        }

        public void Vector3(string name, Vector3 value)
        {
            Raw(name, "[" + FloatText(value.X) + ", " + FloatText(value.Y) + ", " + FloatText(value.Z) + "]");
        }

        public void StringArray(string name, IEnumerable<string> values)
        {
            var parts = new List<string>();
            foreach (var value in values)
            {
                parts.Add(Quote(value));
            }

            Raw(name, "[" + string.Join(", ", parts) + "]");
        }

        public void FloatArray(string name, IEnumerable<float> values)
        {
            var parts = new List<string>();
            foreach (var value in values)
            {
                parts.Add(FloatText(value));
            }

            Raw(name, "[" + string.Join(", ", parts) + "]");
        }

        public void Embed(string name, string value)
        {
            var indent = new string(' ', _depth * 2);
            Raw(name, value.TrimEnd('\n').Replace("\n", "\n" + indent));
        }

        public void BeginObject(string name)
        {
            Raw(name, "{");
            _depth++;
            _first = true;
        }

        public void BeginArray(string name)
        {
            Raw(name, "[");
            _depth++;
            _first = true;
        }

        public void BeginElement()
        {
            Item("{");
            _depth++;
            _first = true;
        }

        public void Element(string json)
        {
            Item(json);
        }

        public void EndObject()
        {
            Close('}');
        }

        public void EndArray()
        {
            Close(']');
        }

        public string Finish()
        {
            _depth = 0;
            NewLine();
            _builder.Append('}').Append('\n');
            return _builder.ToString();
        }

        public static string Quote(string value)
        {
            var builder = new StringBuilder("\"");
            foreach (var c in value ?? string.Empty)
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

            return builder.Append('"').ToString();
        }

        public static string FloatText(float value)
        {
            // Unity の Mono は "R" でも読み直して別の値になる字句を返すことがあるので、そのときは必ず戻る 9 桁で書く。
            var text = value.ToString("R", CultureInfo.InvariantCulture);
            if (float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
            {
                text = value.ToString("G9", CultureInfo.InvariantCulture);
            }

            return text;
        }

        public static string DoubleText(double value)
        {
            // FloatText と同じく、"R" が読み直して別の値になる字句を返したときは必ず戻る 17 桁で書く。
            var text = value.ToString("R", CultureInfo.InvariantCulture);
            if (double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
            {
                text = value.ToString("G17", CultureInfo.InvariantCulture);
            }

            return text;
        }

        // 一覧の JSON (MutatorJson) の中に書いたルールの一部を、GameRulesJson.Parse に渡せる 1 つの文書として取り出すために、読んだ木を字句のまま 1 行に書き戻す。
        public static string NodeText(JsonNode node)
        {
            if (node == null)
            {
                throw new System.ArgumentNullException(nameof(node));
            }

            var builder = new StringBuilder();
            AppendNode(builder, node);
            return builder.ToString();
        }

        private static void AppendNode(StringBuilder builder, JsonNode node)
        {
            switch (node.Kind)
            {
                case JsonNodeKind.Object:
                    builder.Append('{');
                    for (var i = 0; i < node.Members.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        builder.Append(Quote(node.Members[i].Key)).Append(": ");
                        AppendNode(builder, node.Members[i].Value);
                    }

                    builder.Append('}');
                    return;
                case JsonNodeKind.Array:
                    builder.Append('[');
                    for (var i = 0; i < node.Elements.Count; i++)
                    {
                        if (i > 0)
                        {
                            builder.Append(", ");
                        }

                        AppendNode(builder, node.Elements[i]);
                    }

                    builder.Append(']');
                    return;
                case JsonNodeKind.String:
                    builder.Append(Quote(node.Text));
                    return;
                case JsonNodeKind.Number:
                    builder.Append(node.Text);
                    return;
                case JsonNodeKind.True:
                    builder.Append("true");
                    return;
                case JsonNodeKind.False:
                    builder.Append("false");
                    return;
                default:
                    builder.Append("null");
                    return;
            }
        }

        private void Close(char bracket)
        {
            _depth--;
            NewLine();
            _builder.Append(bracket);
            _first = false;
        }

        private void Raw(string name, string value)
        {
            Item(Quote(name) + ": " + value);
        }

        private void Item(string text)
        {
            if (!_first)
            {
                _builder.Append(',');
            }

            NewLine();
            _builder.Append(text);
            _first = false;
        }

        private void NewLine()
        {
            _builder.Append('\n').Append(' ', _depth * 2);
        }
    }
}

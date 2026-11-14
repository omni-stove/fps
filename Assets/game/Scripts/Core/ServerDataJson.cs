using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fps.Core
{
    internal static class ServerDataJson
    {
        public static GameRulesJson.Node ParseDocument(string text, string source, string kind)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            try
            {
                return new GameRulesJson.Parser(text).ParseDocument();
            }
            catch (FormatException e)
            {
                throw new ServerDataJsonException(source, null, $"{kind} '{source}' is not valid JSON: {e.Message}", e);
            }
        }

        public static string SourceOf(string sourceName, string kind)
        {
            return string.IsNullOrWhiteSpace(sourceName) ? "(unnamed " + kind + ")" : sourceName;
        }

        public sealed class Fields
        {
            private static readonly char[] _nonIntegerMarks = { '.', 'e', 'E' };

            private readonly Dictionary<string, GameRulesJson.Node> _values =
                new Dictionary<string, GameRulesJson.Node>(StringComparer.Ordinal);

            public Fields(string source, string kind, GameRulesJson.Node node, string path, IReadOnlyList<string> known)
            {
                Source = source;
                Kind = kind;
                Path = path;
                if (node.Kind != GameRulesJson.NodeKind.Object)
                {
                    var what = path.Length == 0 ? "the top level" : $"item '{path}'";
                    throw new ServerDataJsonException(
                        source, path.Length == 0 ? null : path, $"{kind} '{source}': {what} must be an object, but is {node.Describe()} ({node.Position})");
                }

                var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
                foreach (var member in node.Members)
                {
                    if (!knownSet.Contains(member.Key))
                    {
                        var item = ItemPath(member.Key);
                        throw new ServerDataJsonException(
                            source, item, $"{kind} '{source}': unknown item '{item}' ({member.Value.Position}); known items here are {string.Join(", ", known)}");
                    }

                    _values.Add(member.Key, member.Value);
                }
            }

            public string Source { get; }

            public string Kind { get; }

            public string Path { get; }

            public string ItemPath(string name)
            {
                return Path.Length == 0 ? name : Path + "." + name;
            }

            public bool Has(string name)
            {
                return _values.ContainsKey(name);
            }

            public GameRulesJson.Node Find(string name)
            {
                if (!_values.TryGetValue(name, out var node))
                {
                    return null;
                }

                if (node.Kind == GameRulesJson.NodeKind.Null)
                {
                    throw Invalid(ItemPath(name), node, "must not be null; leave the item out to use the default");
                }

                return node;
            }

            public GameRulesJson.Node Require(string name)
            {
                var node = Find(name);
                if (node == null)
                {
                    var item = ItemPath(name);
                    throw new ServerDataJsonException(Source, item, $"{Kind} '{Source}' has no item '{item}'");
                }

                return node;
            }

            public Fields Object(string name, IReadOnlyList<string> known)
            {
                var node = Find(name);
                return node == null ? null : new Fields(Source, Kind, node, ItemPath(name), known);
            }

            public IReadOnlyList<GameRulesJson.Node> Array(string name)
            {
                var node = Find(name);
                if (node == null)
                {
                    return System.Array.Empty<GameRulesJson.Node>();
                }

                Expect(ItemPath(name), node, GameRulesJson.NodeKind.Array, "an array");
                return node.Elements;
            }

            public Fields Element(GameRulesJson.Node node, string arrayName, int index, IReadOnlyList<string> known)
            {
                return new Fields(Source, Kind, node, ElementPath(arrayName, index), known);
            }

            public string ElementPath(string arrayName, int index)
            {
                return ItemPath(arrayName) + "[" + index.ToString(CultureInfo.InvariantCulture) + "]";
            }

            public float ReadFloat(string name, float fallback)
            {
                var node = Find(name);
                return node == null ? fallback : ToFloat(ItemPath(name), node);
            }

            public int ReadInt(string name, int fallback)
            {
                var node = Find(name);
                return node == null ? fallback : ToInt(ItemPath(name), node);
            }

            public bool ReadBool(string name, bool fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                if (node.Kind == GameRulesJson.NodeKind.True)
                {
                    return true;
                }

                if (node.Kind == GameRulesJson.NodeKind.False)
                {
                    return false;
                }

                throw Invalid(ItemPath(name), node, $"must be true or false, but is {node.Describe()}");
            }

            public string ReadString(string name, string fallback)
            {
                var node = Find(name);
                return node == null ? fallback : ToText(ItemPath(name), node);
            }

            public T ReadEnum<T>(string name, T fallback)
                where T : struct, Enum
            {
                var node = Find(name);
                return node == null ? fallback : ToEnum<T>(ItemPath(name), node);
            }

            public Vector3 ReadVector3(string name)
            {
                var item = ItemPath(name);
                var node = Require(name);
                Expect(item, node, GameRulesJson.NodeKind.Array, "an array of 3 numbers");
                if (node.Elements.Count != 3)
                {
                    throw Invalid(item, node, $"must have 3 numbers (x, y, z), but has {node.Elements.Count.ToString(CultureInfo.InvariantCulture)}");
                }

                return new Vector3(
                    ToFloat(item + "[0]", node.Elements[0]),
                    ToFloat(item + "[1]", node.Elements[1]),
                    ToFloat(item + "[2]", node.Elements[2]));
            }

            public float ToFloat(string item, GameRulesJson.Node node)
            {
                Expect(item, node, GameRulesJson.NodeKind.Number, "a number");

                // GameRulesJson と同じく double を経ずに float へ直す。無限大になる大きすぎる値は各型の検査が項目名付きで断る。
                try
                {
                    return float.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                catch (OverflowException e)
                {
                    throw Invalid(item, node, "is too large for a 32-bit float", e);
                }
            }

            public int ToInt(string item, GameRulesJson.Node node)
            {
                Expect(item, node, GameRulesJson.NodeKind.Number, "a whole number");
                if (node.Text.IndexOfAny(_nonIntegerMarks) >= 0)
                {
                    throw Invalid(item, node, "must be a whole number without a fraction or exponent");
                }

                if (!int.TryParse(node.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                {
                    throw Invalid(item, node, "is out of the 32-bit integer range");
                }

                return value;
            }

            public string ToText(string item, GameRulesJson.Node node)
            {
                Expect(item, node, GameRulesJson.NodeKind.String, "a string");
                return node.Text;
            }

            public T ToEnum<T>(string item, GameRulesJson.Node node)
                where T : struct, Enum
            {
                var text = ToText(item, node);

                // 数を名前として書いたもの ("1") を Enum.TryParse は通すので、名前だけを受け付ける。
                foreach (var name in Enum.GetNames(typeof(T)))
                {
                    if (string.Equals(name, text, StringComparison.Ordinal))
                    {
                        return Enum.Parse<T>(name);
                    }
                }

                throw Invalid(item, node, $"must be one of {string.Join(", ", Enum.GetNames(typeof(T)))}, but is \"{text}\"");
            }

            public ServerDataJsonException Rejected(string item, string problem, Exception inner = null)
            {
                var where = item == null ? string.Empty : $" item '{item}'";
                return new ServerDataJsonException(Source, item, $"{Kind} '{Source}'{where} is invalid: {problem}", inner);
            }

            public ServerDataJsonException Invalid(string item, GameRulesJson.Node node, string problem, Exception inner = null)
            {
                return new ServerDataJsonException(Source, item, $"{Kind} '{Source}' item '{item}' {problem} ({node.Position})", inner);
            }

            private void Expect(string item, GameRulesJson.Node node, GameRulesJson.NodeKind kind, string expected)
            {
                if (node.Kind != kind)
                {
                    throw Invalid(item, node, $"must be {expected}, but is {node.Describe()}");
                }
            }
        }

        public sealed class Writer
        {
            private readonly StringBuilder _builder = new StringBuilder("{");
            private int _depth = 1;
            private bool _first = true;

            public void Float(string name, float value)
            {
                Raw(name, FloatText(value));
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
                // GameRulesJson と同じく、Unity の Mono の "R" が読み直して別の値になるときは必ず戻る 9 桁で書く。
                var text = value.ToString("R", CultureInfo.InvariantCulture);
                if (float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
                {
                    text = value.ToString("G9", CultureInfo.InvariantCulture);
                }

                return text;
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

    public sealed class ServerDataJsonException : Exception
    {
        public ServerDataJsonException()
        {
        }

        public ServerDataJsonException(string message)
            : base(message)
        {
        }

        public ServerDataJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ServerDataJsonException(string sourceName, string item, string message, Exception innerException = null)
            : base(message, innerException)
        {
            SourceName = sourceName;
            Item = item;
        }

        public string SourceName { get; }

        public string Item { get; }
    }
}

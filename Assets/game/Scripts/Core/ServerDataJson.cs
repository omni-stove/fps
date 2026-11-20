using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    internal static class ServerDataJson
    {
        public static JsonNode ParseDocument(string text, string source, string kind)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            try
            {
                return new JsonParser(text).ParseDocument();
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

        // Fields は ServerDataJsonException で断るので、GameRulesJsonException や TuningJsonException で知らせてきた読み手はここで包み直す。
        public static T Rethrown<T>(Func<T> read, Func<ServerDataJsonException, Exception> rewrap)
        {
            if (read == null)
            {
                throw new ArgumentNullException(nameof(read));
            }

            if (rewrap == null)
            {
                throw new ArgumentNullException(nameof(rewrap));
            }

            try
            {
                return read();
            }
            catch (ServerDataJsonException e)
            {
                throw rewrap(e);
            }
        }

        // 読み手ごとに以前から出してきたエラーの文を変えないために、言い回しの違うところだけを選ばせる。
        public sealed class Wording
        {
            public Wording(string knownItemsLead, bool enumErrorsListNames)
            {
                if (string.IsNullOrEmpty(knownItemsLead))
                {
                    throw new ArgumentException("knownItemsLead must not be empty", nameof(knownItemsLead));
                }

                KnownItemsLead = knownItemsLead;
                EnumErrorsListNames = enumErrorsListNames;
            }

            public static Wording Default { get; } = new Wording("known items here are", false);

            public string KnownItemsLead { get; }

            public bool EnumErrorsListNames { get; }
        }

        public sealed class Fields
        {
            private static readonly char[] _nonIntegerMarks = { '.', 'e', 'E' };

            private readonly Dictionary<string, JsonNode> _values =
                new Dictionary<string, JsonNode>(StringComparer.Ordinal);

            public Fields(string source, string kind, JsonNode node, string path, IReadOnlyList<string> known)
                : this(source, kind, node, path, known, Wording.Default)
            {
            }

            public Fields(string source, string kind, JsonNode node, string path, IReadOnlyList<string> known, Wording wording)
            {
                Source = source;
                Kind = kind;
                Path = path;
                Words = wording ?? throw new ArgumentNullException(nameof(wording));
                if (node.Kind != JsonNodeKind.Object)
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
                            source, item, $"{kind} '{source}': unknown item '{item}' ({member.Value.Position}); {Words.KnownItemsLead} {string.Join(", ", known)}");
                    }

                    _values.Add(member.Key, member.Value);
                }
            }

            public string Source { get; }

            public string Kind { get; }

            public string Path { get; }

            public Wording Words { get; }

            public string ItemPath(string name)
            {
                return Path.Length == 0 ? name : Path + "." + name;
            }

            public bool Has(string name)
            {
                return _values.ContainsKey(name);
            }

            // Find は null を書いた項目を断るので、null を「書かない」と同じに読む古い形の項目はこちらで生のまま見る。
            public JsonNode FindRaw(string name)
            {
                return _values.TryGetValue(name, out var node) ? node : null;
            }

            public JsonNode Find(string name)
            {
                if (!_values.TryGetValue(name, out var node))
                {
                    return null;
                }

                if (node.Kind == JsonNodeKind.Null)
                {
                    throw Invalid(ItemPath(name), node, "must not be null; leave the item out to use the default");
                }

                return node;
            }

            public JsonNode Require(string name)
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
                return node == null ? null : new Fields(Source, Kind, node, ItemPath(name), known, Words);
            }

            public IReadOnlyList<JsonNode> Array(string name)
            {
                var node = Find(name);
                if (node == null)
                {
                    return System.Array.Empty<JsonNode>();
                }

                Expect(ItemPath(name), node, JsonNodeKind.Array, "an array");
                return node.Elements;
            }

            public Fields Element(JsonNode node, string arrayName, int index, IReadOnlyList<string> known)
            {
                return new Fields(Source, Kind, node, ElementPath(arrayName, index), known, Words);
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

            public double ReadDouble(string name, double fallback)
            {
                var node = Find(name);
                return node == null ? fallback : ToDouble(ItemPath(name), node);
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

                if (node.Kind == JsonNodeKind.True)
                {
                    return true;
                }

                if (node.Kind == JsonNodeKind.False)
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

            // 空の配列と重なりはここで断らず、値を受け取る型の検査に任せて項目名付きで断らせる。
            // 要素の誤りを番号を付けずに配列の項目名で示すのは、GameRulesJson が以前からその項目名で知らせてきたため。
            public IReadOnlyList<T> ReadEnumArray<T>(string name, IReadOnlyList<T> fallback)
                where T : struct, Enum
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                var item = ItemPath(name);
                Expect(item, node, JsonNodeKind.Array, "an array of the names " + string.Join(", ", Enum.GetNames(typeof(T))));
                var values = new T[node.Elements.Count];
                for (var i = 0; i < values.Length; i++)
                {
                    values[i] = ToEnum<T>(item, node.Elements[i]);
                }

                return values;
            }

            public Vector3 ReadVector3(string name)
            {
                var item = ItemPath(name);
                var node = Require(name);
                Expect(item, node, JsonNodeKind.Array, "an array of 3 numbers");
                if (node.Elements.Count != 3)
                {
                    throw Invalid(item, node, $"must have 3 numbers (x, y, z), but has {node.Elements.Count.ToString(CultureInfo.InvariantCulture)}");
                }

                return new Vector3(
                    ToFloat(item + "[0]", node.Elements[0]),
                    ToFloat(item + "[1]", node.Elements[1]),
                    ToFloat(item + "[2]", node.Elements[2]));
            }

            public float ToFloat(string item, JsonNode node)
            {
                Expect(item, node, JsonNodeKind.Number, "a number");

                // 2 進の float の最も近い値を直接選ぶ (double を経ると丸めが 2 度起きて C# の float リテラルと 1 ビットずれうる)。
                // 大きすぎる値は無限大になる環境と例外になる環境があり、無限大なら各型の検査が項目名付きで断る。
                try
                {
                    return float.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                catch (OverflowException e)
                {
                    throw Invalid(item, node, "is too large for a 32-bit float", e);
                }
            }

            public double ToDouble(string item, JsonNode node)
            {
                Expect(item, node, JsonNodeKind.Number, "a number");

                // 大きすぎる値は無限大になる環境と例外になる環境があり、無限大なら各型の検査が項目名付きで断る。
                try
                {
                    return double.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                catch (OverflowException e)
                {
                    throw Invalid(item, node, "is too large for a 64-bit float", e);
                }
            }

            public int ToInt(string item, JsonNode node)
            {
                Expect(item, node, JsonNodeKind.Number, "a whole number");
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

            public string ToText(string item, JsonNode node)
            {
                Expect(item, node, JsonNodeKind.String, "a string");
                return node.Text;
            }

            public T ToEnum<T>(string item, JsonNode node)
                where T : struct, Enum
            {
                var names = Enum.GetNames(typeof(T));
                if (Words.EnumErrorsListNames)
                {
                    Expect(item, node, JsonNodeKind.String, "one of the names " + string.Join(", ", names));
                }

                var text = ToText(item, node);

                // 数を名前として書いたもの ("1") を Enum.TryParse は通すので、名前だけを受け付ける。
                foreach (var name in names)
                {
                    if (string.Equals(name, text, StringComparison.Ordinal))
                    {
                        return Enum.Parse<T>(name);
                    }
                }

                if (Words.EnumErrorsListNames)
                {
                    throw Invalid(item, node, $"\"{text}\" is not one of {string.Join(", ", names)}");
                }

                throw Invalid(item, node, $"must be one of {string.Join(", ", names)}, but is \"{text}\"");
            }

            public ServerDataJsonException Rejected(string item, string problem, Exception inner = null)
            {
                var where = item == null ? string.Empty : $" item '{item}'";
                return new ServerDataJsonException(Source, item, $"{Kind} '{Source}'{where} is invalid: {problem}", inner);
            }

            public ServerDataJsonException Invalid(string item, JsonNode node, string problem, Exception inner = null)
            {
                return new ServerDataJsonException(Source, item, $"{Kind} '{Source}' item '{item}' {problem} ({node.Position})", inner);
            }

            private void Expect(string item, JsonNode node, JsonNodeKind kind, string expected)
            {
                if (node.Kind != kind)
                {
                    throw Invalid(item, node, $"must be {expected}, but is {node.Describe()}");
                }
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

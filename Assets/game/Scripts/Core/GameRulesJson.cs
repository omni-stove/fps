using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="GameRules"/> を JSON から読む。項目は <see cref="GameRules"/> の構成と同じで、名前はコンストラクターの引数名
    /// (<see cref="ItemNames"/>)。アクションは <see cref="ActionsItem"/> の入れ子のオブジェクト (<see cref="ActionItemNames"/>) に書く。
    /// 省略した項目は <see cref="GameRules.Default"/> (アクションは <see cref="GameRules.Default"/> の <see cref="GameRules.Actions"/>) の値になる。
    /// 列挙は名前の文字列 (例: <c>"Off"</c>)、小数は JSON の数、整数は小数点も指数も無い数、真偽は true / false で書く。
    /// 値の範囲の検査は <see cref="GameRules"/> と <see cref="ActionRules"/> に任せ、どれかに引っかかれば
    /// どのファイルのどの項目かを添えた <see cref="GameRulesJsonException"/> を投げる。
    /// </summary>
    public static class GameRulesJson
    {
        public const string ActionsItem = "actions";

        public const string FriendlyFireItem = "friendlyFire";
        public const string RespawnModeItem = "respawnMode";
        public const string WaveIntervalSecondsItem = "waveIntervalSeconds";
        public const string RegenDelaySecondsItem = "regenDelaySeconds";
        public const string RegenPerSecondItem = "regenPerSecond";
        public const string DeathBodyItem = "deathBody";
        public const string DeathCameraItem = "deathCamera";
        public const string InfiniteReserveItem = "infiniteReserve";
        public const string ReserveAmmoMultiplierItem = "reserveAmmoMultiplier";
        public const string DestructionItem = "destruction";
        public const string SpawnProtectionSecondsItem = "spawnProtectionSeconds";
        public const string FallDamageItem = "fallDamage";
        public const string HudDensityItem = "hudDensity";
        public const string ObjectiveItem = "objective";
        public const string MinPlayersItem = "minPlayers";
        public const string IdleKickSecondsItem = "idleKickSeconds";

        public const string DownedItem = "downed";
        public const string DownedBleedoutSecondsItem = "downedBleedoutSeconds";
        public const string DownedHealthFractionItem = "downedHealthFraction";
        public const string ReviveSecondsItem = "reviveSeconds";
        public const string CoverItem = "cover";
        public const string BlindFireItem = "blindFire";
        public const string ActiveReloadItem = "activeReload";
        public const string DodgeItem = "dodge";
        public const string VaultItem = "vault";
        public const string GrenadeItem = "grenade";
        public const string GrenadeCountItem = "grenadeCount";
        public const string MeleeItem = "melee";
        public const string StaggerItem = "stagger";
        public const string SlideItem = "slide";
        public const string MantleItem = "mantle";
        public const string ProneItem = "prone";
        public const string LadderItem = "ladder";
        public const string ZiplineItem = "zipline";
        public const string ParachuteItem = "parachute";
        public const string StealthItem = "stealth";
        public const string SuppressionItem = "suppression";
        public const string PingItem = "ping";
        public const string AbilitiesItem = "abilities";
        public const string SlowMotionItem = "slowMotion";

        /// <summary>入れ子の深さの上限。項目は 2 段までなので、それより深いものは読む前に断る。</summary>
        private const int _maxDepth = 8;

        private const char _byteOrderMark = (char)0xFEFF;

        /// <summary>最上位に書ける項目の名前 (<see cref="GameRules"/> のコンストラクターの並び)。</summary>
        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            FriendlyFireItem,
            RespawnModeItem,
            WaveIntervalSecondsItem,
            RegenDelaySecondsItem,
            RegenPerSecondItem,
            DeathBodyItem,
            DeathCameraItem,
            InfiniteReserveItem,
            ReserveAmmoMultiplierItem,
            DestructionItem,
            ActionsItem,
            SpawnProtectionSecondsItem,
            FallDamageItem,
            HudDensityItem,
            ObjectiveItem,
            MinPlayersItem,
            IdleKickSecondsItem,
        };

        /// <summary><see cref="ActionsItem"/> の中に書ける項目の名前 (<see cref="ActionRules"/> のコンストラクターの並び)。</summary>
        public static IReadOnlyList<string> ActionItemNames { get; } = new[]
        {
            DownedItem,
            DownedBleedoutSecondsItem,
            DownedHealthFractionItem,
            ReviveSecondsItem,
            CoverItem,
            BlindFireItem,
            ActiveReloadItem,
            DodgeItem,
            VaultItem,
            GrenadeItem,
            GrenadeCountItem,
            MeleeItem,
            StaggerItem,
            SlideItem,
            MantleItem,
            ProneItem,
            LadderItem,
            ZiplineItem,
            ParachuteItem,
            StealthItem,
            SuppressionItem,
            PingItem,
            AbilitiesItem,
            SlowMotionItem,
        };

        /// <summary>text を 1 つのルールとして読む。</summary>
        /// <param name="text">JSON の文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="sourceName">例外の文に出す読み込み元の名前 (ファイルのパスなど)。</param>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="GameRulesJsonException">
        /// JSON の文法に合わない、知らない項目がある、型が合わない、または値が <see cref="GameRules"/> の検査に通らない。
        /// </exception>
        public static GameRules Parse(string text, string sourceName)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var source = string.IsNullOrWhiteSpace(sourceName) ? "(unnamed game rules JSON)" : sourceName;

            Node root;
            try
            {
                root = new Parser(text).ParseDocument();
            }
            catch (FormatException e)
            {
                throw new GameRulesJsonException(source, null, $"game rules JSON '{source}' is not valid JSON: {e.Message}", e);
            }

            var top = new Fields(source, root, string.Empty, ItemNames);
            var defaults = GameRules.Default;

            var actions = ReadActions(source, top, defaults.Actions);

            try
            {
                return new GameRules(
                    top.ReadEnum(FriendlyFireItem, defaults.FriendlyFire),
                    top.ReadEnum(RespawnModeItem, defaults.RespawnMode),
                    top.ReadFloat(WaveIntervalSecondsItem, defaults.WaveIntervalSeconds),
                    top.ReadFloat(RegenDelaySecondsItem, defaults.RegenDelaySeconds),
                    top.ReadFloat(RegenPerSecondItem, defaults.RegenPerSecond),
                    top.ReadEnum(DeathBodyItem, defaults.DeathBody),
                    top.ReadEnum(DeathCameraItem, defaults.DeathCamera),
                    top.ReadBool(InfiniteReserveItem, defaults.InfiniteReserve),
                    top.ReadFloat(ReserveAmmoMultiplierItem, defaults.ReserveAmmoMultiplier),
                    top.ReadEnum(DestructionItem, defaults.Destruction),
                    actions,
                    top.ReadFloat(SpawnProtectionSecondsItem, defaults.SpawnProtectionSeconds),
                    top.ReadEnum(FallDamageItem, defaults.FallDamage),
                    top.ReadEnum(HudDensityItem, defaults.HudDensity),
                    top.ReadEnum(ObjectiveItem, defaults.Objective),
                    top.ReadInt(MinPlayersItem, defaults.MinPlayers),
                    top.ReadFloat(IdleKickSecondsItem, defaults.IdleKickSeconds));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, string.Empty, e);
            }
        }

        /// <summary>
        /// rules のすべての項目を <see cref="Parse"/> が読める JSON にする。並びは <see cref="ItemNames"/> と <see cref="ActionItemNames"/> の順で、
        /// 省略はしない。小数は読み直すと同じ float になる最短の形で書くので、<see cref="Parse"/> に通すと同じ値に戻る。
        /// </summary>
        /// <exception cref="ArgumentNullException">rules が null。</exception>
        public static string Write(GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var actions = rules.Actions;
            var writer = new Writer();
            writer.Enum(FriendlyFireItem, rules.FriendlyFire);
            writer.Enum(RespawnModeItem, rules.RespawnMode);
            writer.Float(WaveIntervalSecondsItem, rules.WaveIntervalSeconds);
            writer.Float(RegenDelaySecondsItem, rules.RegenDelaySeconds);
            writer.Float(RegenPerSecondItem, rules.RegenPerSecond);
            writer.Enum(DeathBodyItem, rules.DeathBody);
            writer.Enum(DeathCameraItem, rules.DeathCamera);
            writer.Bool(InfiniteReserveItem, rules.InfiniteReserve);
            writer.Float(ReserveAmmoMultiplierItem, rules.ReserveAmmoMultiplier);
            writer.Enum(DestructionItem, rules.Destruction);
            writer.BeginObject(ActionsItem);
            writer.Enum(DownedItem, actions.Downed);
            writer.Float(DownedBleedoutSecondsItem, actions.DownedBleedoutSeconds);
            writer.Float(DownedHealthFractionItem, actions.DownedHealthFraction);
            writer.Float(ReviveSecondsItem, actions.ReviveSeconds);
            writer.Enum(CoverItem, actions.Cover);
            writer.Enum(BlindFireItem, actions.BlindFire);
            writer.Enum(ActiveReloadItem, actions.ActiveReload);
            writer.Enum(DodgeItem, actions.Dodge);
            writer.Enum(VaultItem, actions.Vault);
            writer.Enum(GrenadeItem, actions.Grenade);
            writer.Int(GrenadeCountItem, actions.GrenadeCount);
            writer.Enum(MeleeItem, actions.Melee);
            writer.Enum(StaggerItem, actions.Stagger);
            writer.Enum(SlideItem, actions.Slide);
            writer.Enum(MantleItem, actions.Mantle);
            writer.Enum(ProneItem, actions.Prone);
            writer.Enum(LadderItem, actions.Ladder);
            writer.Enum(ZiplineItem, actions.Zipline);
            writer.Enum(ParachuteItem, actions.Parachute);
            writer.Enum(StealthItem, actions.Stealth);
            writer.Enum(SuppressionItem, actions.Suppression);
            writer.Enum(PingItem, actions.Ping);
            writer.Enum(AbilitiesItem, actions.Abilities);
            writer.Enum(SlowMotionItem, actions.SlowMotion);
            writer.EndObject();
            writer.Float(SpawnProtectionSecondsItem, rules.SpawnProtectionSeconds);
            writer.Enum(FallDamageItem, rules.FallDamage);
            writer.Enum(HudDensityItem, rules.HudDensity);
            writer.Enum(ObjectiveItem, rules.Objective);
            writer.Int(MinPlayersItem, rules.MinPlayers);
            writer.Float(IdleKickSecondsItem, rules.IdleKickSeconds);
            return writer.Finish();
        }

        private static ActionRules ReadActions(string source, Fields top, ActionRules defaults)
        {
            var node = top.ReadNested(ActionsItem);
            if (node == null)
            {
                return defaults;
            }

            var prefix = ActionsItem + ".";
            var fields = new Fields(source, node, prefix, ActionItemNames);
            try
            {
                return new ActionRules(
                    fields.ReadEnum(DownedItem, defaults.Downed),
                    fields.ReadFloat(DownedBleedoutSecondsItem, defaults.DownedBleedoutSeconds),
                    fields.ReadFloat(DownedHealthFractionItem, defaults.DownedHealthFraction),
                    fields.ReadFloat(ReviveSecondsItem, defaults.ReviveSeconds),
                    fields.ReadEnum(CoverItem, defaults.Cover),
                    fields.ReadEnum(BlindFireItem, defaults.BlindFire),
                    fields.ReadEnum(ActiveReloadItem, defaults.ActiveReload),
                    fields.ReadEnum(DodgeItem, defaults.Dodge),
                    fields.ReadEnum(VaultItem, defaults.Vault),
                    fields.ReadEnum(GrenadeItem, defaults.Grenade),
                    fields.ReadInt(GrenadeCountItem, defaults.GrenadeCount),
                    fields.ReadEnum(MeleeItem, defaults.Melee),
                    fields.ReadEnum(StaggerItem, defaults.Stagger),
                    fields.ReadEnum(SlideItem, defaults.Slide),
                    fields.ReadEnum(MantleItem, defaults.Mantle),
                    fields.ReadEnum(ProneItem, defaults.Prone),
                    fields.ReadEnum(LadderItem, defaults.Ladder),
                    fields.ReadEnum(ZiplineItem, defaults.Zipline),
                    fields.ReadEnum(ParachuteItem, defaults.Parachute),
                    fields.ReadEnum(StealthItem, defaults.Stealth),
                    fields.ReadEnum(SuppressionItem, defaults.Suppression),
                    fields.ReadEnum(PingItem, defaults.Ping),
                    fields.ReadEnum(AbilitiesItem, defaults.Abilities),
                    fields.ReadEnum(SlowMotionItem, defaults.SlowMotion));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, prefix, e);
            }
        }

        // GameRules と ActionRules は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static GameRulesJsonException Rejected(string source, string prefix, ArgumentException e)
        {
            var item = string.IsNullOrEmpty(e.ParamName) ? null : prefix + e.ParamName;
            var where = item == null ? string.Empty : $" item '{item}'";
            return new GameRulesJsonException(source, item, $"game rules JSON '{source}'{where} is invalid: {e.Message}", e);
        }

        // グレネードと印の調整値の JSON (TuningJson) も同じ文法・同じ位置の示し方で読むので、Node と Parser はアセンブリの中に開く。
        internal enum NodeKind
        {
            Object,
            Array,
            String,
            Number,
            True,
            False,
            Null,
        }

        internal sealed class Node
        {
            public Node(NodeKind kind, string text, int line, int column)
            {
                Kind = kind;
                Text = text;
                Line = line;
                Column = column;
            }

            public NodeKind Kind { get; }

            /// <summary>文字列ならその中身、数なら書かれたままの字句。読む側の型 (float / int) で解釈し直すため字句のまま持つ。</summary>
            public string Text { get; }

            public int Line { get; }

            public int Column { get; }

            /// <summary>オブジェクトの項目。書かれた順。</summary>
            public List<KeyValuePair<string, Node>> Members { get; } = new List<KeyValuePair<string, Node>>();

            public string Position => $"line {Line.ToString(CultureInfo.InvariantCulture)}, column {Column.ToString(CultureInfo.InvariantCulture)}";

            public string Describe()
            {
                switch (Kind)
                {
                    case NodeKind.Object:
                        return "an object";
                    case NodeKind.Array:
                        return "an array";
                    case NodeKind.String:
                        return $"the string \"{Text}\"";
                    case NodeKind.Number:
                        return $"the number {Text}";
                    case NodeKind.True:
                        return "true";
                    case NodeKind.False:
                        return "false";
                    default:
                        return "null";
                }
            }
        }

        /// <summary>1 つのオブジェクトの項目を、型を確かめながら取り出す。作った時点で知らない項目が無いことを確かめる。</summary>
        private sealed class Fields
        {
            private static readonly char[] _nonIntegerMarks = { '.', 'e', 'E' };

            private readonly string _source;
            private readonly string _prefix;
            private readonly Dictionary<string, Node> _values = new Dictionary<string, Node>(StringComparer.Ordinal);

            public Fields(string source, Node node, string prefix, IReadOnlyList<string> known)
            {
                _source = source;
                _prefix = prefix;

                if (node.Kind != NodeKind.Object)
                {
                    var what = prefix.Length == 0 ? "the top level" : $"item '{prefix.TrimEnd('.')}'";
                    throw new GameRulesJsonException(
                        source,
                        prefix.Length == 0 ? null : prefix.TrimEnd('.'),
                        $"game rules JSON '{source}': {what} must be an object, but is {node.Describe()} ({node.Position})");
                }

                var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
                foreach (var member in node.Members)
                {
                    if (!knownSet.Contains(member.Key))
                    {
                        throw new GameRulesJsonException(
                            source,
                            prefix + member.Key,
                            $"game rules JSON '{source}': unknown item '{prefix + member.Key}' ({member.Value.Position}); "
                            + $"known items here are {string.Join(", ", known)}");
                    }

                    _values.Add(member.Key, member.Value);
                }
            }

            public Node ReadNested(string name)
            {
                return Find(name);
            }

            public float ReadFloat(string name, float fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                Require(node, name, NodeKind.Number, "a number");

                // 2 進の float の最も近い値を直接選ぶ (double を経ると丸めが 2 度起きて C# の float リテラルと 1 ビットずれうる)。
                // 大きすぎる値は無限大になる環境と例外になる環境があり、無限大なら GameRules の検査が項目名付きで断る。
                try
                {
                    return float.Parse(node.Text, NumberStyles.Float, CultureInfo.InvariantCulture);
                }
                catch (OverflowException e)
                {
                    throw Invalid(name, node, "is too large for a 32-bit float", e);
                }
            }

            public int ReadInt(string name, int fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                Require(node, name, NodeKind.Number, "a whole number");
                if (node.Text.IndexOfAny(_nonIntegerMarks) >= 0)
                {
                    throw Invalid(name, node, "must be a whole number without a fraction or exponent");
                }

                if (!int.TryParse(node.Text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value))
                {
                    throw Invalid(name, node, "is out of the 32-bit integer range");
                }

                return value;
            }

            public bool ReadBool(string name, bool fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                if (node.Kind == NodeKind.True)
                {
                    return true;
                }

                if (node.Kind == NodeKind.False)
                {
                    return false;
                }

                throw Invalid(name, node, $"must be true or false, but is {node.Describe()}");
            }

            public T ReadEnum<T>(string name, T fallback)
                where T : struct, Enum
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                var names = System.Enum.GetNames(typeof(T));
                Require(node, name, NodeKind.String, "one of the names " + string.Join(", ", names));

                // 数字の文字列や大文字小文字違いも Enum.TryParse は通してしまうので、定義済みの名前と完全に一致するものだけを受け付ける。
                foreach (var candidate in names)
                {
                    if (string.Equals(candidate, node.Text, StringComparison.Ordinal))
                    {
                        return (T)System.Enum.Parse<T>(candidate);
                    }
                }

                throw Invalid(name, node, $"\"{node.Text}\" is not one of {string.Join(", ", names)}");
            }

            private Node Find(string name)
            {
                if (!_values.TryGetValue(name, out var node))
                {
                    return null;
                }

                if (node.Kind == NodeKind.Null)
                {
                    throw Invalid(name, node, "must not be null; leave the item out to use the default");
                }

                return node;
            }

            private void Require(Node node, string name, NodeKind kind, string expected)
            {
                if (node.Kind != kind)
                {
                    throw Invalid(name, node, $"must be {expected}, but is {node.Describe()}");
                }
            }

            private GameRulesJsonException Invalid(string name, Node node, string problem, Exception inner = null)
            {
                var item = _prefix + name;
                return new GameRulesJsonException(
                    _source, item, $"game rules JSON '{_source}' item '{item}' {problem} ({node.Position})", inner);
            }
        }

        /// <summary>項目を書いた順に、2 文字の字下げで 1 行 1 項目に並べる。項目名と列挙の名前は英数字だけなので、文字列の逃がしは要らない。</summary>
        private sealed class Writer
        {
            private readonly StringBuilder _builder = new StringBuilder("{");
            private int _depth = 1;
            private bool _first = true;

            public void Enum<T>(string name, T value)
                where T : struct, System.Enum
            {
                Raw(name, "\"" + value.ToString() + "\"");
            }

            public void Float(string name, float value)
            {
                // Unity の Mono は "R" でも読み直して別の値になる字句を返すことがあるので、そのときは必ず戻る 9 桁で書く。
                var text = value.ToString("R", CultureInfo.InvariantCulture);
                if (float.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture) != value)
                {
                    text = value.ToString("G9", CultureInfo.InvariantCulture);
                }

                Raw(name, text);
            }

            public void Int(string name, int value)
            {
                Raw(name, value.ToString(CultureInfo.InvariantCulture));
            }

            public void Bool(string name, bool value)
            {
                Raw(name, value ? "true" : "false");
            }

            public void BeginObject(string name)
            {
                Raw(name, "{");
                _depth++;
                _first = true;
            }

            public void EndObject()
            {
                _depth--;
                NewLine();
                _builder.Append('}');
                _first = false;
            }

            public string Finish()
            {
                _depth = 0;
                NewLine();
                _builder.Append('}').Append('\n');
                return _builder.ToString();
            }

            private void Raw(string name, string value)
            {
                if (!_first)
                {
                    _builder.Append(',');
                }

                NewLine();
                _builder.Append('"').Append(name).Append("\": ").Append(value);
                _first = false;
            }

            private void NewLine()
            {
                _builder.Append('\n').Append(' ', _depth * 2);
            }
        }

        /// <summary>JSON (RFC 8259) をそのまま読む。コメントや末尾のカンマなどの拡張は受け付けない。</summary>
        internal sealed class Parser
        {
            private readonly string _text;
            private int _position;
            private int _line = 1;
            private int _lineStart;

            public Parser(string text)
            {
                _text = text;
            }

            public Node ParseDocument()
            {
                if (_position < _text.Length && _text[_position] == _byteOrderMark)
                {
                    _position++;
                    _lineStart = _position;
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

            private Node ParseValue(int depth)
            {
                if (_position >= _text.Length)
                {
                    throw Error("unexpected end of input");
                }

                var line = _line;
                var column = Column;
                var c = _text[_position];
                switch (c)
                {
                    case '{':
                        return ParseObject(depth + 1, line, column);
                    case '[':
                        return ParseArray(depth + 1, line, column);
                    case '"':
                        return new Node(NodeKind.String, ParseString(), line, column);
                    case 't':
                        ExpectLiteral("true");
                        return new Node(NodeKind.True, null, line, column);
                    case 'f':
                        ExpectLiteral("false");
                        return new Node(NodeKind.False, null, line, column);
                    case 'n':
                        ExpectLiteral("null");
                        return new Node(NodeKind.Null, null, line, column);
                    default:
                        if (c == '-' || IsDigit(c))
                        {
                            return new Node(NodeKind.Number, ParseNumber(), line, column);
                        }

                        throw Error($"unexpected character '{c}'");
                }
            }

            private Node ParseObject(int depth, int line, int column)
            {
                CheckDepth(depth);
                _position++;
                var node = new Node(NodeKind.Object, null, line, column);
                var keys = new HashSet<string>(StringComparer.Ordinal);
                SkipWhitespace();
                if (TryConsume('}'))
                {
                    return node;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (_position >= _text.Length || _text[_position] != '"')
                    {
                        throw Error("expected a string key");
                    }

                    var keyLine = _line;
                    var keyColumn = Column;
                    var key = ParseString();
                    if (!keys.Add(key))
                    {
                        throw new FormatException(
                            $"duplicate key \"{key}\" at line {keyLine.ToString(CultureInfo.InvariantCulture)}, "
                            + $"column {keyColumn.ToString(CultureInfo.InvariantCulture)}");
                    }

                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    node.Members.Add(new KeyValuePair<string, Node>(key, ParseValue(depth)));
                    SkipWhitespace();
                    if (TryConsume('}'))
                    {
                        return node;
                    }

                    Expect(',');
                }
            }

            private Node ParseArray(int depth, int line, int column)
            {
                CheckDepth(depth);
                _position++;
                var node = new Node(NodeKind.Array, null, line, column);
                SkipWhitespace();
                if (TryConsume(']'))
                {
                    return node;
                }

                // 配列を値に取る項目は無いので、中身は文法だけ確かめて捨てる。型の誤りは Fields が項目名付きで知らせる。
                while (true)
                {
                    SkipWhitespace();
                    ParseValue(depth);
                    SkipWhitespace();
                    if (TryConsume(']'))
                    {
                        return node;
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

                    var c = _text[_position];
                    if (c == '"')
                    {
                        _position++;
                        return builder.ToString();
                    }

                    if (c < ' ')
                    {
                        throw Error("control character in string");
                    }

                    _position++;
                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (_position >= _text.Length)
                    {
                        throw Error("unterminated string");
                    }

                    var escape = _text[_position];
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
                            _position++;
                            builder.Append(ParseHex4());
                            continue;
                        default:
                            throw Error($"invalid escape '\\{escape}'");
                    }

                    _position++;
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
                    var c = _text[_position];
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
                    _position++;
                }

                return (char)value;
            }

            /// <summary>JSON の数の文法どおりに字句を切り出して返す。値への変換は読む側の型に任せる。</summary>
            private string ParseNumber()
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

                if (TryConsume('.'))
                {
                    RequireDigits();
                }

                if (TryConsume('e') || TryConsume('E'))
                {
                    if (!TryConsume('+'))
                    {
                        TryConsume('-');
                    }

                    RequireDigits();
                }

                return _text.Substring(start, _position - start);
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
                    if (c == '\n')
                    {
                        _line++;
                        _lineStart = _position + 1;
                    }
                    else if (c != ' ' && c != '\t' && c != '\r')
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

            private int Column => _position - _lineStart + 1;

            private FormatException Error(string message)
            {
                var at = _position >= _text.Length
                    ? "end of input"
                    : $"line {_line.ToString(CultureInfo.InvariantCulture)}, column {Column.ToString(CultureInfo.InvariantCulture)}";
                return new FormatException(message + " at " + at);
            }

            private static bool IsDigit(char c)
            {
                return c >= '0' && c <= '9';
            }
        }
    }

    /// <summary>ルールの JSON が読めない、または値が不正。<see cref="SourceName"/> と <see cref="Item"/> でどこが悪いかを示す。</summary>
    public sealed class GameRulesJsonException : Exception
    {
        public GameRulesJsonException()
        {
        }

        public GameRulesJsonException(string message)
            : base(message)
        {
        }

        public GameRulesJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public GameRulesJsonException(string sourceName, string item, string message, Exception innerException = null)
            : base(message, innerException)
        {
            SourceName = sourceName;
            Item = item;
        }

        /// <summary>読み込み元の名前 (ファイルのパスなど)。</summary>
        public string SourceName { get; }

        /// <summary>悪い項目の名前 (入れ子は <c>actions.grenadeCount</c> の形)。JSON の文法の誤りなど、項目に結び付かないときは null。</summary>
        public string Item { get; }
    }
}

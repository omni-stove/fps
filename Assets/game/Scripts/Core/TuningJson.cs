using System;
using System.Collections.Generic;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// グレネード (<see cref="GrenadeSpec"/>) と印 (<see cref="PingSpec"/>) の調整値を JSON から読む。
    /// どちらも入れ子の無い 1 つのオブジェクトで、項目の名前はコンストラクターの引数名 (<see cref="GrenadeItemNames"/>・<see cref="PingItemNames"/>)。
    /// 省略した項目は <see cref="GrenadeSpec.Default"/>・<see cref="PingSpec.Default"/> の値になる。小数は JSON の数、整数は小数点も指数も無い数で書く。
    /// 文法は <see cref="GameRulesJson"/> と同じで、値の範囲の検査は Spec に任せ、どれかに引っかかれば
    /// どのファイルのどの項目かを添えた <see cref="TuningJsonException"/> を投げる。
    /// </summary>
    public static class TuningJson
    {
        public const string FuseSecondsItem = "fuseSeconds";
        public const string ThrowSecondsItem = "throwSeconds";
        public const string ThrowSpeedItem = "throwSpeed";
        public const string ThrowLiftSpeedItem = "throwLiftSpeed";
        public const string MaxDamageItem = "maxDamage";
        public const string DamageRadiusItem = "damageRadius";
        public const string FullDamageRadiusItem = "fullDamageRadius";
        public const string StructureDamageAtCenterItem = "structureDamageAtCenter";

        public const string LifetimeSecondsItem = "lifetimeSeconds";
        public const string MaxMarkersItem = "maxMarkers";
        public const string MaxDistanceItem = "maxDistance";
        public const string EnemyAimToleranceDegreesItem = "enemyAimToleranceDegrees";
        public const string MinIntervalSecondsItem = "minIntervalSeconds";

        private const string _grenadeKind = "grenade JSON";
        private const string _pingKind = "ping JSON";

        /// <summary>グレネードの JSON に書ける項目の名前 (<see cref="GrenadeSpec"/> のコンストラクターの並び)。</summary>
        public static IReadOnlyList<string> GrenadeItemNames { get; } = new[]
        {
            FuseSecondsItem,
            ThrowSecondsItem,
            ThrowSpeedItem,
            ThrowLiftSpeedItem,
            MaxDamageItem,
            DamageRadiusItem,
            FullDamageRadiusItem,
            StructureDamageAtCenterItem,
        };

        /// <summary>印の JSON に書ける項目の名前 (<see cref="PingSpec"/> のコンストラクターの並び)。</summary>
        public static IReadOnlyList<string> PingItemNames { get; } = new[]
        {
            LifetimeSecondsItem,
            MaxMarkersItem,
            MaxDistanceItem,
            EnemyAimToleranceDegreesItem,
            MinIntervalSecondsItem,
        };

        /// <summary>text を 1 つのグレネードの調整値として読む。</summary>
        /// <param name="text">JSON の文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="sourceName">例外の文に出す読み込み元の名前 (ファイルのパスなど)。</param>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="TuningJsonException">
        /// JSON の文法に合わない、知らない項目がある、型が合わない、または値が <see cref="GrenadeSpec"/> の検査に通らない。
        /// </exception>
        public static GrenadeSpec ParseGrenade(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _grenadeKind);
            var fields = Fields.Read(text, source, _grenadeKind, GrenadeItemNames);
            var defaults = GrenadeSpec.Default;
            try
            {
                return new GrenadeSpec(
                    fields.ReadFloat(FuseSecondsItem, defaults.FuseSeconds),
                    fields.ReadFloat(ThrowSecondsItem, defaults.ThrowSeconds),
                    fields.ReadFloat(ThrowSpeedItem, defaults.ThrowSpeed),
                    fields.ReadFloat(ThrowLiftSpeedItem, defaults.ThrowLiftSpeed),
                    fields.ReadInt(MaxDamageItem, defaults.MaxDamage),
                    fields.ReadFloat(DamageRadiusItem, defaults.DamageRadius),
                    fields.ReadFloat(FullDamageRadiusItem, defaults.FullDamageRadius),
                    fields.ReadInt(StructureDamageAtCenterItem, defaults.StructureDamageAtCenter));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, _grenadeKind, GrenadeItemNames, e);
            }
        }

        /// <summary>text を 1 つの印の調整値として読む。</summary>
        /// <param name="text">JSON の文字列。先頭の BOM は読み飛ばす。</param>
        /// <param name="sourceName">例外の文に出す読み込み元の名前 (ファイルのパスなど)。</param>
        /// <exception cref="ArgumentNullException">text が null。</exception>
        /// <exception cref="TuningJsonException">
        /// JSON の文法に合わない、知らない項目がある、型が合わない、または値が <see cref="PingSpec"/> の検査に通らない。
        /// </exception>
        public static PingSpec ParsePing(string text, string sourceName)
        {
            var source = SourceOf(sourceName, _pingKind);
            var fields = Fields.Read(text, source, _pingKind, PingItemNames);
            var defaults = PingSpec.Default;
            try
            {
                return new PingSpec(
                    fields.ReadFloat(LifetimeSecondsItem, defaults.LifetimeSeconds),
                    fields.ReadInt(MaxMarkersItem, defaults.MaxMarkers),
                    fields.ReadFloat(MaxDistanceItem, defaults.MaxDistance),
                    fields.ReadFloat(EnemyAimToleranceDegreesItem, defaults.EnemyAimToleranceDegrees),
                    fields.ReadFloat(MinIntervalSecondsItem, defaults.MinIntervalSeconds));
            }
            catch (ArgumentException e)
            {
                throw Rejected(source, _pingKind, PingItemNames, e);
            }
        }

        private static string SourceOf(string sourceName, string kind)
        {
            return string.IsNullOrWhiteSpace(sourceName) ? "(unnamed " + kind + ")" : sourceName;
        }

        // Spec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        // 内側の ExplosionDamage など、項目に無い名前で投げられたときは項目に結び付けない。
        private static TuningJsonException Rejected(string source, string kind, IReadOnlyList<string> known, ArgumentException e)
        {
            string item = null;
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    item = name;
                    break;
                }
            }

            var where = item == null ? string.Empty : $" item '{item}'";
            return new TuningJsonException(source, item, $"{kind} '{source}'{where} is invalid: {e.Message}", e);
        }

        /// <summary>最上位のオブジェクトの項目を、型を確かめながら取り出す。作った時点で知らない項目が無いことを確かめる。</summary>
        private sealed class Fields
        {
            private static readonly char[] _nonIntegerMarks = { '.', 'e', 'E' };

            private readonly string _source;
            private readonly string _kind;
            private readonly Dictionary<string, GameRulesJson.Node> _values =
                new Dictionary<string, GameRulesJson.Node>(StringComparer.Ordinal);

            private Fields(string source, string kind)
            {
                _source = source;
                _kind = kind;
            }

            public static Fields Read(string text, string source, string kind, IReadOnlyList<string> known)
            {
                if (text == null)
                {
                    throw new ArgumentNullException(nameof(text));
                }

                GameRulesJson.Node root;
                try
                {
                    root = new GameRulesJson.Parser(text).ParseDocument();
                }
                catch (FormatException e)
                {
                    throw new TuningJsonException(source, null, $"{kind} '{source}' is not valid JSON: {e.Message}", e);
                }

                if (root.Kind != GameRulesJson.NodeKind.Object)
                {
                    throw new TuningJsonException(
                        source, null, $"{kind} '{source}': the top level must be an object, but is {root.Describe()} ({root.Position})");
                }

                var fields = new Fields(source, kind);
                var knownSet = new HashSet<string>(known, StringComparer.Ordinal);
                foreach (var member in root.Members)
                {
                    if (!knownSet.Contains(member.Key))
                    {
                        throw new TuningJsonException(
                            source,
                            member.Key,
                            $"{kind} '{source}': unknown item '{member.Key}' ({member.Value.Position}); "
                            + $"known items are {string.Join(", ", known)}");
                    }

                    fields._values.Add(member.Key, member.Value);
                }

                return fields;
            }

            public float ReadFloat(string name, float fallback)
            {
                var node = Find(name);
                if (node == null)
                {
                    return fallback;
                }

                Require(node, name, "a number");

                // GameRulesJson と同じく double を経ずに float へ直す。無限大になる大きすぎる値は Spec の検査が項目名付きで断る。
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

                Require(node, name, "a whole number");
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

            private GameRulesJson.Node Find(string name)
            {
                if (!_values.TryGetValue(name, out var node))
                {
                    return null;
                }

                if (node.Kind == GameRulesJson.NodeKind.Null)
                {
                    throw Invalid(name, node, "must not be null; leave the item out to use the default");
                }

                return node;
            }

            private void Require(GameRulesJson.Node node, string name, string expected)
            {
                if (node.Kind != GameRulesJson.NodeKind.Number)
                {
                    throw Invalid(name, node, $"must be {expected}, but is {node.Describe()}");
                }
            }

            private TuningJsonException Invalid(string name, GameRulesJson.Node node, string problem, Exception inner = null)
            {
                return new TuningJsonException(_source, name, $"{_kind} '{_source}' item '{name}' {problem} ({node.Position})", inner);
            }
        }
    }

    /// <summary>グレネードか印の調整値の JSON が読めない、または値が不正。<see cref="SourceName"/> と <see cref="Item"/> でどこが悪いかを示す。</summary>
    public sealed class TuningJsonException : Exception
    {
        public TuningJsonException()
        {
        }

        public TuningJsonException(string message)
            : base(message)
        {
        }

        public TuningJsonException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public TuningJsonException(string sourceName, string item, string message, Exception innerException = null)
            : base(message, innerException)
        {
            SourceName = sourceName;
            Item = item;
        }

        /// <summary>読み込み元の名前 (ファイルのパスなど)。</summary>
        public string SourceName { get; }

        /// <summary>悪い項目の名前。JSON の文法の誤りなど、項目に結び付かないときは null。</summary>
        public string Item { get; }
    }
}

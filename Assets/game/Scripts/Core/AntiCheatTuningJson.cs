using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class AntiCheatTuningJson
    {
        public const string ConcealItem = "conceal";
        public const string ViolationsItem = "violations";
        public const string StatsItem = "stats";
        public const string EvidenceItem = "evidence";

        public const string EnabledItem = "enabled";
        public const string MaxDistanceItem = "maxDistance";
        public const string MarginDistanceItem = "marginDistance";
        public const string MarginSecondsItem = "marginSeconds";
        public const string WindowSecondsItem = "windowSeconds";
        public const string ShotRejectionLimitItem = "shotRejectionLimit";
        public const string OverBudgetLimitItem = "overBudgetLimit";
        public const string HeadshotRatioItem = "headshotRatio";
        public const string MinReactionMsItem = "minReactionMs";
        public const string SnapDegreesPerTickItem = "snapDegreesPerTick";
        public const string WallTrackSecondsItem = "wallTrackSeconds";
        public const string SecondsItem = "seconds";

        private const string _kind = "anticheat JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { ConcealItem, ViolationsItem, StatsItem, EvidenceItem };

        // 項目名を各設定のコンストラクターの引数名にそろえ、値の検査で断られた項目を名前で示せるようにする。
        public static IReadOnlyList<string> ConcealItemNames { get; } = new[]
        {
            EnabledItem,
            MaxDistanceItem,
            MarginDistanceItem,
            MarginSecondsItem,
        };

        public static IReadOnlyList<string> ViolationItemNames { get; } = new[]
        {
            EnabledItem,
            WindowSecondsItem,
            ShotRejectionLimitItem,
            OverBudgetLimitItem,
        };

        public static IReadOnlyList<string> StatsItemNames { get; } = new[]
        {
            EnabledItem,
            WindowSecondsItem,
            HeadshotRatioItem,
            MinReactionMsItem,
            SnapDegreesPerTickItem,
            WallTrackSecondsItem,
        };

        public static IReadOnlyList<string> EvidenceItemNames { get; } = new[] { EnabledItem, SecondsItem };

        public static AntiCheatTuning Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var root = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                return new AntiCheatTuning(
                    ReadConceal(root.Object(ConcealItem, ConcealItemNames)),
                    ReadViolations(root.Object(ViolationsItem, ViolationItemNames)),
                    ReadStats(root.Object(StatsItem, StatsItemNames)),
                    ReadEvidence(root.Object(EvidenceItem, EvidenceItemNames)));
            }
            catch (ServerDataJsonException e)
            {
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static AntiCheatTuning.ConcealSettings ReadConceal(ServerDataJson.Fields fields)
        {
            var defaults = AntiCheatTuning.ConcealSettings.Default;
            if (fields == null)
            {
                return defaults;
            }

            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var maxDistance = fields.ReadFloat(MaxDistanceItem, defaults.MaxDistance);
            var marginDistance = fields.ReadFloat(MarginDistanceItem, defaults.MarginDistance);
            var marginSeconds = fields.ReadFloat(MarginSecondsItem, defaults.MarginSeconds);
            try
            {
                return new AntiCheatTuning.ConcealSettings(enabled, maxDistance, marginDistance, marginSeconds);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ConcealItemNames, e);
            }
        }

        private static AntiCheatTuning.ViolationSettings ReadViolations(ServerDataJson.Fields fields)
        {
            var defaults = AntiCheatTuning.ViolationSettings.Default;
            if (fields == null)
            {
                return defaults;
            }

            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var window = fields.ReadFloat(WindowSecondsItem, defaults.WindowSeconds);
            var shotRejections = fields.ReadInt(ShotRejectionLimitItem, defaults.ShotRejectionLimit);
            var overBudget = fields.ReadInt(OverBudgetLimitItem, defaults.OverBudgetLimit);
            try
            {
                return new AntiCheatTuning.ViolationSettings(enabled, window, shotRejections, overBudget);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ViolationItemNames, e);
            }
        }

        private static AntiCheatTuning.StatsSettings ReadStats(ServerDataJson.Fields fields)
        {
            var defaults = AntiCheatTuning.StatsSettings.Default;
            if (fields == null)
            {
                return defaults;
            }

            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var window = fields.ReadFloat(WindowSecondsItem, defaults.WindowSeconds);
            var headshotRatio = fields.ReadFloat(HeadshotRatioItem, defaults.HeadshotRatio);
            var minReaction = fields.ReadFloat(MinReactionMsItem, defaults.MinReactionMs);
            var snap = fields.ReadFloat(SnapDegreesPerTickItem, defaults.SnapDegreesPerTick);
            var wallTrack = fields.ReadFloat(WallTrackSecondsItem, defaults.WallTrackSeconds);
            try
            {
                return new AntiCheatTuning.StatsSettings(enabled, window, headshotRatio, minReaction, snap, wallTrack);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, StatsItemNames, e);
            }
        }

        private static AntiCheatTuning.EvidenceSettings ReadEvidence(ServerDataJson.Fields fields)
        {
            var defaults = AntiCheatTuning.EvidenceSettings.Default;
            if (fields == null)
            {
                return defaults;
            }

            var enabled = fields.ReadBool(EnabledItem, defaults.Enabled);
            var seconds = fields.ReadFloat(SecondsItem, defaults.Seconds);
            try
            {
                return new AntiCheatTuning.EvidenceSettings(enabled, seconds);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, EvidenceItemNames, e);
            }
        }

        private static ServerDataJsonException Rejected(ServerDataJson.Fields fields, IReadOnlyList<string> names, ArgumentException e)
        {
            var item = e.ParamName != null && Contains(names, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path;
            return fields.Rejected(item, e.Message, e);
        }

        private static bool Contains(IReadOnlyList<string> names, string name)
        {
            foreach (var candidate in names)
            {
                if (string.Equals(candidate, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

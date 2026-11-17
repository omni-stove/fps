using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class StreakJson
    {
        public const string ResourcePath = "Tuning/streaks";

        public const string RewardsItem = "rewards";
        public const string UavItem = "uav";
        public const string AirstrikeItem = "airstrike";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string KillsRequiredItem = "killsRequired";

        public const string DurationSecondsItem = "durationSeconds";
        public const string RangeItem = "range";

        public const string ShellCountItem = "shellCount";
        public const string IntervalSecondsItem = "intervalSeconds";
        public const string SpreadItem = "spread";
        public const string ExplosionItem = "explosion";

        // 武器の JSON の explosion と同じ名前にし、同じ書き方で爆発を書けるようにする。
        public const string MaxDamageItem = WeaponSpecJson.MaxDamageItem;
        public const string RadiusItem = WeaponSpecJson.RadiusItem;
        public const string FullDamageRadiusItem = WeaponSpecJson.FullDamageRadiusItem;
        public const string StructureDamageAtCenterItem = WeaponSpecJson.StructureDamageAtCenterItem;

        private const string _kind = "streak JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { RewardsItem, UavItem, AirstrikeItem };

        // 項目名はコンストラクターの引数名にそろえ、値の検査で断られた項目を ParamName から示せるようにする。
        public static IReadOnlyList<string> RewardItemNames { get; } = new[] { IdItem, NameKeyItem, KillsRequiredItem };

        public static IReadOnlyList<string> UavItemNames { get; } = new[] { DurationSecondsItem, RangeItem };

        public static IReadOnlyList<string> AirstrikeItemNames { get; } = new[] { ShellCountItem, IntervalSecondsItem, SpreadItem, ExplosionItem, RangeItem };

        // 空爆は落ちた所で爆ぜるので、武器の explosion にある detonation は受け付けない。
        public static IReadOnlyList<string> ExplosionItemNames { get; } = new[]
        {
            MaxDamageItem,
            RadiusItem,
            FullDamageRadiusItem,
            StructureDamageAtCenterItem,
        };

        public static StreakSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                var rewards = fields.Find(RewardsItem) == null ? null : ReadRewards(fields);
                var uav = fields.Object(UavItem, UavItemNames);
                var airstrike = fields.Object(AirstrikeItem, AirstrikeItemNames);
                var uavSpec = uav == null ? null : ReadUav(uav);
                var airstrikeSpec = airstrike == null ? null : ReadAirstrike(airstrike);
                try
                {
                    return new StreakSpec(rewards, uavSpec, airstrikeSpec);
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(RewardsItem, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static List<StreakReward> ReadRewards(ServerDataJson.Fields fields)
        {
            var nodes = fields.Array(RewardsItem);
            var rewards = new List<StreakReward>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var entry = fields.Element(nodes[i], RewardsItem, i, RewardItemNames);
                var id = entry.ToText(entry.ItemPath(IdItem), entry.Require(IdItem));
                var nameKey = entry.ReadString(NameKeyItem, null);
                var killsRequired = entry.ToInt(entry.ItemPath(KillsRequiredItem), entry.Require(KillsRequiredItem));
                try
                {
                    rewards.Add(new StreakReward(id, nameKey, killsRequired));
                }
                catch (ArgumentException e)
                {
                    throw Rejected(entry, RewardItemNames, e);
                }
            }

            return rewards;
        }

        private static StreakUavSpec ReadUav(ServerDataJson.Fields fields)
        {
            var durationSeconds = fields.ReadFloat(DurationSecondsItem, StreakUavSpec.DefaultDurationSeconds);
            var range = fields.ReadFloat(RangeItem, StreakUavSpec.DefaultRange);
            try
            {
                return new StreakUavSpec(durationSeconds, range);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, UavItemNames, e);
            }
        }

        private static StreakAirstrikeSpec ReadAirstrike(ServerDataJson.Fields fields)
        {
            var shellCount = fields.ReadInt(ShellCountItem, StreakAirstrikeSpec.DefaultShellCount);
            var intervalSeconds = fields.ReadFloat(IntervalSecondsItem, StreakAirstrikeSpec.DefaultIntervalSeconds);
            var spread = fields.ReadFloat(SpreadItem, StreakAirstrikeSpec.DefaultSpread);
            var explosionFields = fields.Object(ExplosionItem, ExplosionItemNames);
            var explosion = explosionFields == null ? null : ReadExplosion(explosionFields);
            var range = fields.ReadFloat(RangeItem, StreakAirstrikeSpec.DefaultRange);
            try
            {
                return new StreakAirstrikeSpec(shellCount, intervalSeconds, spread, explosion, range);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, AirstrikeItemNames, e);
            }
        }

        private static ExplosionSpec ReadExplosion(ServerDataJson.Fields fields)
        {
            var maxDamage = fields.ReadInt(MaxDamageItem, StreakAirstrikeSpec.DefaultMaxDamage);
            var radius = fields.ReadFloat(RadiusItem, StreakAirstrikeSpec.DefaultRadius);
            var fullDamageRadius = fields.ReadFloat(FullDamageRadiusItem, StreakAirstrikeSpec.DefaultFullDamageRadius);
            var structureDamageAtCenter = fields.ReadInt(StructureDamageAtCenterItem, StreakAirstrikeSpec.DefaultStructureDamageAtCenter);
            try
            {
                return new ExplosionSpec(new ExplosionDamage(maxDamage, radius, fullDamageRadius), structureDamageAtCenter);
            }
            catch (ArgumentException e)
            {
                throw Rejected(fields, ExplosionItemNames, e);
            }
        }

        private static ServerDataJsonException Rejected(ServerDataJson.Fields fields, IReadOnlyList<string> known, ArgumentException e)
        {
            foreach (var name in known)
            {
                if (string.Equals(name, e.ParamName, StringComparison.Ordinal))
                {
                    return fields.Rejected(fields.ItemPath(name), e.Message, e);
                }
            }

            return fields.Rejected(fields.Path, e.Message, e);
        }
    }
}

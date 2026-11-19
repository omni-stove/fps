using System.Collections.Generic;

namespace Shooter.Core
{
    public static class ZoneJson
    {
        public const string PhasesItem = "phases";
        public const string DamageIntervalSecondsItem = "damageIntervalSeconds";
        public const string SeedItem = "seed";

        public const string WaitSecondsItem = "waitSeconds";
        public const string ShrinkSecondsItem = "shrinkSeconds";
        public const string RadiusItem = "radius";
        public const string DamagePerSecondItem = "damagePerSecond";

        private const string _kind = "zone JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { PhasesItem, DamageIntervalSecondsItem, SeedItem };

        public static IReadOnlyList<string> PhaseItemNames { get; } = new[] { WaitSecondsItem, ShrinkSecondsItem, RadiusItem, DamagePerSecondItem };

        public static ZoneSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, Read);
        }

        // 別の調整値のファイルの中の節 (battle-royale.json の zone) からも同じ項目で読めるよう、節を受け取る口を分ける
        internal static ZoneSpec Read(ServerDataJson.Fields fields)
        {
            var phases = SpecJsonReader.ReadList(fields, PhasesItem, PhaseItemNames, ReadPhase);
            var interval = fields.ReadFloat(DamageIntervalSecondsItem, ZoneSpec.DefaultDamageIntervalSeconds);
            var seedNode = fields.Find(SeedItem);
            var seed = seedNode == null ? (int?)null : fields.ToInt(fields.ItemPath(SeedItem), seedNode);
            return SpecJsonReader.Build(fields, ItemNames, () => new ZoneSpec(phases, interval, seed));
        }

        // 段階の値は 1 つ書き忘れると円とダメージの釣り合いが崩れるので、どれも省けない
        private static ZonePhase ReadPhase(ServerDataJson.Fields phase)
        {
            var waitSeconds = phase.ToFloat(phase.ItemPath(WaitSecondsItem), phase.Require(WaitSecondsItem));
            var shrinkSeconds = phase.ToFloat(phase.ItemPath(ShrinkSecondsItem), phase.Require(ShrinkSecondsItem));
            var radius = phase.ToFloat(phase.ItemPath(RadiusItem), phase.Require(RadiusItem));
            var damagePerSecond = phase.ToFloat(phase.ItemPath(DamagePerSecondItem), phase.Require(DamagePerSecondItem));
            return new ZonePhase(waitSeconds, shrinkSeconds, radius, damagePerSecond);
        }
    }
}

using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 倒れた人が落とす物 (PickupDropSpec) の JSON。書いていない項目は PickupDropSpec の既定のまま
    public static class PickupDropJson
    {
        public const string DropsWeaponItem = "dropsWeapon";
        public const string ReserveAmmoItem = "reserveAmmo";
        public const string LifetimeSecondsItem = "lifetimeSeconds";
        public const string ReachItem = "reach";
        public const string SpreadRadiusItem = "spreadRadius";

        private const string _kind = "pickup drops JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            DropsWeaponItem,
            ReserveAmmoItem,
            LifetimeSecondsItem,
            ReachItem,
            SpreadRadiusItem,
        };

        public static PickupDropSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new PickupDropSpec(
                        fields.ReadBool(DropsWeaponItem, PickupDropSpec.DefaultDropsWeapon),
                        fields.ReadInt(ReserveAmmoItem, PickupDropSpec.DefaultReserveAmmo),
                        fields.ReadFloat(LifetimeSecondsItem, PickupDropSpec.DefaultLifetimeSeconds),
                        fields.ReadFloat(ReachItem, PickupDropSpec.DefaultReach),
                        fields.ReadFloat(SpreadRadiusItem, PickupDropSpec.DefaultSpreadRadius));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(Known(e.ParamName) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // サーバーの読み手 (ServerRulesData) は TuningJsonException だけを「調整値が読めない」として扱う
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        public static string Write(PickupDropSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var writer = new JsonWriter();
            writer.Bool(DropsWeaponItem, spec.DropsWeapon);
            writer.Int(ReserveAmmoItem, spec.ReserveAmmo);
            writer.Float(LifetimeSecondsItem, spec.LifetimeSeconds);
            writer.Float(ReachItem, spec.Reach);
            writer.Float(SpreadRadiusItem, spec.SpreadRadius);
            return writer.Finish();
        }

        // PickupDropSpec は引数名を ParamName に入れて投げ、引数名は項目名と同じなので、そのまま項目名として返せる。
        private static bool Known(string name)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

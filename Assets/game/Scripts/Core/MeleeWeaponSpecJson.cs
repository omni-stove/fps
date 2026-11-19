using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 武器の JSON の melee を読み書きする。振りの項目名は近接の調整値 (MeleeJson) と同じにし、同じ値を同じ名前で書けるようにする。
    internal static class MeleeWeaponSpecJson
    {
        public const string MeleeItem = "melee";
        public const string SwingItem = "swing";
        public const string HeavySwingItem = "heavySwing";
        public const string ChargeDelaySecondsItem = "chargeDelaySeconds";
        public const string ChargeSecondsItem = "chargeSeconds";
        public const string ThrowItem = "throw";

        public const string ThrowDamageItem = "damage";
        public const string ThrowSpeedItem = "speed";
        public const string ThrowUpwardDegreesItem = "upwardDegrees";
        public const string ThrowSpinDegreesPerSecondItem = "spinDegreesPerSecond";
        public const string ThrowRadiusItem = "radius";
        public const string ThrowMaxFlightSecondsItem = "maxFlightSeconds";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            SwingItem, HeavySwingItem, ChargeDelaySecondsItem, ChargeSecondsItem, ThrowItem,
        };

        // 近接武器の振りは技 (処刑技・背後から倒す技) を出さないので、振りの値だけを書かせる。
        public static IReadOnlyList<string> SwingItemNames { get; } = RiotShieldSpecJson.BashItemNames;

        public static IReadOnlyList<string> ThrowItemNames { get; } = new[]
        {
            ThrowDamageItem, ThrowSpeedItem, ThrowUpwardDegreesItem, ThrowSpinDegreesPerSecondItem, ThrowRadiusItem, ThrowMaxFlightSecondsItem,
        };

        public static MeleeWeaponSpec Read(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            var swing = ReadSwing(fields.Object(SwingItem, SwingItemNames), MeleeWeaponSpec.DefaultSwing);

            // 溜め切った振りの省いた項目は、ふつうの振りの値で埋める。ダメージだけ書けば強くなるように。
            var heavyFields = fields.Object(HeavySwingItem, SwingItemNames);
            var heavy = heavyFields == null ? null : ReadSwing(heavyFields, swing);
            var throwFields = fields.Object(ThrowItem, ThrowItemNames);
            var thrown = throwFields == null ? null : ReadThrow(throwFields);
            try
            {
                return new MeleeWeaponSpec(
                    swing,
                    heavy,
                    fields.ReadFloat(ChargeDelaySecondsItem, MeleeWeaponSpec.DefaultChargeDelaySeconds),
                    fields.ReadFloat(ChargeSecondsItem, MeleeWeaponSpec.DefaultChargeSeconds),
                    thrown);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(ItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        public static void Write(ServerDataJson.Writer writer, MeleeWeaponSpec melee)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // 近接武器でない武器の JSON を、この項目を足す前と同じ文字列のまま書き戻す。
            if (melee == null)
            {
                return;
            }

            writer.BeginObject(MeleeItem);
            WriteSwing(writer, SwingItem, melee.Swing);
            if (melee.HeavySwing != null)
            {
                WriteSwing(writer, HeavySwingItem, melee.HeavySwing);
            }

            writer.Float(ChargeDelaySecondsItem, melee.ChargeDelaySeconds);
            writer.Float(ChargeSecondsItem, melee.ChargeSeconds);
            if (melee.Throw != null)
            {
                writer.BeginObject(ThrowItem);
                writer.Int(ThrowDamageItem, melee.Throw.Damage);
                writer.Float(ThrowSpeedItem, melee.Throw.Speed);
                writer.Float(ThrowUpwardDegreesItem, melee.Throw.UpwardDegrees);
                writer.Float(ThrowSpinDegreesPerSecondItem, melee.Throw.SpinDegreesPerSecond);
                writer.Float(ThrowRadiusItem, melee.Throw.Radius);
                writer.Float(ThrowMaxFlightSecondsItem, melee.Throw.MaxFlightSeconds);
                writer.EndObject();
            }

            writer.EndObject();
        }

        private static void WriteSwing(ServerDataJson.Writer writer, string item, MeleeSpec swing)
        {
            writer.BeginObject(item);
            writer.Int(MeleeJson.DamageItem, swing.Damage);
            writer.Float(MeleeJson.ReachItem, swing.Reach);
            writer.Float(MeleeJson.ArcDegreesItem, swing.ArcDegrees);
            writer.Float(MeleeJson.SwingSecondsItem, swing.SwingSeconds);
            writer.Float(MeleeJson.CooldownSecondsItem, swing.CooldownSeconds);
            writer.Float(MeleeJson.ImpactSecondsItem, swing.ImpactSeconds);
            writer.Float(MeleeJson.LungeDistanceItem, swing.LungeDistance);
            writer.EndObject();
        }

        private static MeleeSpec ReadSwing(ServerDataJson.Fields fields, MeleeSpec defaults)
        {
            if (fields == null)
            {
                return defaults;
            }

            try
            {
                return new MeleeSpec(
                    fields.ReadInt(MeleeJson.DamageItem, defaults.Damage),
                    fields.ReadFloat(MeleeJson.ReachItem, defaults.Reach),
                    fields.ReadFloat(MeleeJson.ArcDegreesItem, defaults.ArcDegrees),
                    fields.ReadFloat(MeleeJson.SwingSecondsItem, defaults.SwingSeconds),
                    fields.ReadFloat(MeleeJson.CooldownSecondsItem, defaults.CooldownSeconds),
                    fields.ReadFloat(MeleeJson.ImpactSecondsItem, defaults.ImpactSeconds),
                    fields.ReadFloat(MeleeJson.LungeDistanceItem, defaults.LungeDistance));
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(SwingItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        private static MeleeThrowSpec ReadThrow(ServerDataJson.Fields fields)
        {
            var defaults = MeleeThrowSpec.Default;
            try
            {
                return new MeleeThrowSpec(
                    fields.ReadInt(ThrowDamageItem, defaults.Damage),
                    fields.ReadFloat(ThrowSpeedItem, defaults.Speed),
                    fields.ReadFloat(ThrowUpwardDegreesItem, defaults.UpwardDegrees),
                    fields.ReadFloat(ThrowSpinDegreesPerSecondItem, defaults.SpinDegreesPerSecond),
                    fields.ReadFloat(ThrowRadiusItem, defaults.Radius),
                    fields.ReadFloat(ThrowMaxFlightSecondsItem, defaults.MaxFlightSeconds));
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(ThrowItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        private static bool Known(IReadOnlyList<string> names, string item)
        {
            foreach (var name in names)
            {
                if (string.Equals(name, item, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 武器の JSON の shield を読み書きする。殴りの項目名は近接の調整値 (MeleeJson) と同じにし、同じ値を同じ名前で書けるようにする。
    internal static class RiotShieldSpecJson
    {
        public const string ShieldItem = "shield";
        public const string BlockArcDegreesItem = "blockArcDegrees";
        public const string WidthItem = "width";
        public const string HeightItem = "height";
        public const string MoveSpeedMultiplierItem = "moveSpeedMultiplier";
        public const string BashItem = "bash";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            BlockArcDegreesItem, WidthItem, HeightItem, MoveSpeedMultiplierItem, BashItem,
        };

        // 盾の殴りは技 (処刑技・背後から倒す技) を出さないので、振りの値だけを書かせる。
        public static IReadOnlyList<string> BashItemNames { get; } = new[]
        {
            MeleeJson.DamageItem,
            MeleeJson.ReachItem,
            MeleeJson.ArcDegreesItem,
            MeleeJson.SwingSecondsItem,
            MeleeJson.CooldownSecondsItem,
            MeleeJson.ImpactSecondsItem,
            MeleeJson.LungeDistanceItem,
        };

        public static RiotShieldSpec Read(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            var bash = ReadBash(fields.Object(BashItem, BashItemNames));
            try
            {
                return new RiotShieldSpec(
                    fields.ReadFloat(BlockArcDegreesItem, RiotShieldSpec.DefaultBlockArcDegrees),
                    fields.ReadFloat(WidthItem, RiotShieldSpec.DefaultWidth),
                    fields.ReadFloat(HeightItem, RiotShieldSpec.DefaultHeight),
                    fields.ReadFloat(MoveSpeedMultiplierItem, RiotShieldSpec.DefaultMoveSpeedMultiplier),
                    bash);
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(ItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
            }
        }

        public static void Write(ServerDataJson.Writer writer, RiotShieldSpec shield)
        {
            if (writer == null)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            // 盾でない武器の JSON を、この項目を足す前と同じ文字列のまま書き戻す。
            if (shield == null)
            {
                return;
            }

            writer.BeginObject(ShieldItem);
            writer.Float(BlockArcDegreesItem, shield.BlockArcDegrees);
            writer.Float(WidthItem, shield.Width);
            writer.Float(HeightItem, shield.Height);
            writer.Float(MoveSpeedMultiplierItem, shield.MoveSpeedMultiplier);
            writer.BeginObject(BashItem);
            writer.Int(MeleeJson.DamageItem, shield.Bash.Damage);
            writer.Float(MeleeJson.ReachItem, shield.Bash.Reach);
            writer.Float(MeleeJson.ArcDegreesItem, shield.Bash.ArcDegrees);
            writer.Float(MeleeJson.SwingSecondsItem, shield.Bash.SwingSeconds);
            writer.Float(MeleeJson.CooldownSecondsItem, shield.Bash.CooldownSeconds);
            writer.Float(MeleeJson.ImpactSecondsItem, shield.Bash.ImpactSeconds);
            writer.Float(MeleeJson.LungeDistanceItem, shield.Bash.LungeDistance);
            writer.EndObject();
            writer.EndObject();
        }

        private static MeleeSpec ReadBash(ServerDataJson.Fields fields)
        {
            if (fields == null)
            {
                return null;
            }

            var defaults = RiotShieldSpec.DefaultBash;
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
                throw fields.Rejected(Known(BashItemNames, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
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

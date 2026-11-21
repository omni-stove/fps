using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // bots.json の種類ごとの値 (shield・sniper・charger・bomber) を読む。項目名は各 Spec のコンストラクターの引数名にそろえ、
    // 値の検査で断られた項目を名前で示せるようにする。盾の殴りの項目名は近接の調整値 (MeleeJson) と同じにする
    internal static class BotArchetypeJson
    {
        public const string ShieldItem = "shield";
        public const string SniperItem = "sniper";
        public const string ChargerItem = "charger";
        public const string BomberItem = "bomber";

        public const string ApproachSpeedMultiplierItem = "approachSpeedMultiplier";
        public const string HoldDistanceItem = "holdDistance";
        public const string MeleeLowerSecondsItem = "meleeLowerSeconds";
        public const string ExplosionLowerSecondsItem = "explosionLowerSeconds";
        public const string BashItem = "bash";

        public const string AimSecondsItem = "aimSeconds";
        public const string ShotDamageMultiplierItem = "shotDamageMultiplier";
        public const string ReaimDelaySecondsItem = "reaimDelaySeconds";
        public const string RelocateGiveUpSecondsItem = "relocateGiveUpSeconds";

        public const string TriggerDistanceItem = "triggerDistance";
        public const string WindupSecondsItem = "windupSeconds";
        public const string ChargeSpeedItem = "chargeSpeed";
        public const string ChargeDistanceItem = "chargeDistance";
        public const string HitReachItem = "hitReach";
        public const string ImpactDamageItem = "impactDamage";
        public const string StunSecondsItem = "stunSeconds";
        public const string RecoverSecondsItem = "recoverSeconds";

        public const string FuseSecondsItem = "fuseSeconds";
        public const string RunSpeedMultiplierItem = "runSpeedMultiplier";
        public const string BlastItem = "blast";
        public const string DeathBlastItem = "deathBlast";
        public const string MaxDamageItem = "maxDamage";
        public const string RadiusItem = "radius";
        public const string FullDamageRadiusItem = "fullDamageRadius";

        public static IReadOnlyList<string> ShieldItemNames { get; } = new[]
        {
            ApproachSpeedMultiplierItem, HoldDistanceItem, MeleeLowerSecondsItem, ExplosionLowerSecondsItem, BashItem,
        };

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

        public static IReadOnlyList<string> SniperItemNames { get; } = new[]
        {
            AimSecondsItem, ShotDamageMultiplierItem, ReaimDelaySecondsItem, RelocateGiveUpSecondsItem,
        };

        public static IReadOnlyList<string> ChargerItemNames { get; } = new[]
        {
            TriggerDistanceItem,
            WindupSecondsItem,
            ChargeSpeedItem,
            ChargeDistanceItem,
            HitReachItem,
            ImpactDamageItem,
            StunSecondsItem,
            RecoverSecondsItem,
        };

        public static IReadOnlyList<string> BomberItemNames { get; } = new[]
        {
            TriggerDistanceItem, FuseSecondsItem, RunSpeedMultiplierItem, BlastItem, DeathBlastItem,
        };

        public static IReadOnlyList<string> BlastItemNames { get; } = new[] { MaxDamageItem, RadiusItem, FullDamageRadiusItem };

        public static ShieldSpec ReadShield(ServerDataJson.Fields parent, ShieldSpec fallback)
        {
            var fields = parent.Object(ShieldItem, ShieldItemNames);
            if (fields == null)
            {
                return fallback;
            }

            var bash = ReadBash(fields.Object(BashItem, BashItemNames), fallback.Bash);
            return Build(fields, ShieldItemNames, () => new ShieldSpec(
                fields.ReadFloat(ApproachSpeedMultiplierItem, fallback.ApproachSpeedMultiplier),
                fields.ReadFloat(HoldDistanceItem, fallback.HoldDistance),
                fields.ReadFloat(MeleeLowerSecondsItem, fallback.MeleeLowerSeconds),
                fields.ReadFloat(ExplosionLowerSecondsItem, fallback.ExplosionLowerSeconds),
                bash));
        }

        public static SniperSpec ReadSniper(ServerDataJson.Fields parent, SniperSpec fallback)
        {
            var fields = parent.Object(SniperItem, SniperItemNames);
            if (fields == null)
            {
                return fallback;
            }

            return Build(fields, SniperItemNames, () => new SniperSpec(
                fields.ReadFloat(AimSecondsItem, fallback.AimSeconds),
                fields.ReadFloat(ShotDamageMultiplierItem, fallback.ShotDamageMultiplier),
                fields.ReadFloat(ReaimDelaySecondsItem, fallback.ReaimDelaySeconds),
                fields.ReadFloat(RelocateGiveUpSecondsItem, fallback.RelocateGiveUpSeconds)));
        }

        public static ChargerSpec ReadCharger(ServerDataJson.Fields parent, ChargerSpec fallback)
        {
            var fields = parent.Object(ChargerItem, ChargerItemNames);
            if (fields == null)
            {
                return fallback;
            }

            return Build(fields, ChargerItemNames, () => new ChargerSpec(
                fields.ReadFloat(TriggerDistanceItem, fallback.TriggerDistance),
                fields.ReadFloat(WindupSecondsItem, fallback.WindupSeconds),
                fields.ReadFloat(ChargeSpeedItem, fallback.ChargeSpeed),
                fields.ReadFloat(ChargeDistanceItem, fallback.ChargeDistance),
                fields.ReadFloat(HitReachItem, fallback.HitReach),
                fields.ReadInt(ImpactDamageItem, fallback.ImpactDamage),
                fields.ReadFloat(StunSecondsItem, fallback.StunSeconds),
                fields.ReadFloat(RecoverSecondsItem, fallback.RecoverSeconds)));
        }

        public static BomberSpec ReadBomber(ServerDataJson.Fields parent, BomberSpec fallback)
        {
            var fields = parent.Object(BomberItem, BomberItemNames);
            if (fields == null)
            {
                return fallback;
            }

            var blast = ReadBlast(fields.Object(BlastItem, BlastItemNames), fallback.Blast);
            var deathBlast = ReadBlast(fields.Object(DeathBlastItem, BlastItemNames), fallback.DeathBlast);
            return Build(fields, BomberItemNames, () => new BomberSpec(
                fields.ReadFloat(TriggerDistanceItem, fallback.TriggerDistance),
                fields.ReadFloat(FuseSecondsItem, fallback.FuseSeconds),
                fields.ReadFloat(RunSpeedMultiplierItem, fallback.RunSpeedMultiplier),
                blast,
                deathBlast));
        }

        private static MeleeSpec ReadBash(ServerDataJson.Fields fields, MeleeSpec fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            return Build(fields, BashItemNames, () => new MeleeSpec(
                fields.ReadInt(MeleeJson.DamageItem, fallback.Damage),
                fields.ReadFloat(MeleeJson.ReachItem, fallback.Reach),
                fields.ReadFloat(MeleeJson.ArcDegreesItem, fallback.ArcDegrees),
                fields.ReadFloat(MeleeJson.SwingSecondsItem, fallback.SwingSeconds),
                fields.ReadFloat(MeleeJson.CooldownSecondsItem, fallback.CooldownSeconds),
                fields.ReadFloat(MeleeJson.ImpactSecondsItem, fallback.ImpactSeconds),
                fields.ReadFloat(MeleeJson.LungeDistanceItem, fallback.LungeDistance)));
        }

        // ExplosionDamage の引数名 (maxDamage・radius・fullDamageRadius) は項目名と同じなので、そのまま項目に結び付く
        private static ExplosionDamage ReadBlast(ServerDataJson.Fields fields, ExplosionDamage fallback)
        {
            if (fields == null)
            {
                return fallback;
            }

            return Build(fields, BlastItemNames, () => new ExplosionDamage(
                fields.ReadInt(MaxDamageItem, fallback.MaxDamage),
                fields.ReadFloat(RadiusItem, fallback.Radius),
                fields.ReadFloat(FullDamageRadiusItem, fallback.FullDamageRadius)));
        }

        private static T Build<T>(ServerDataJson.Fields fields, IReadOnlyList<string> known, Func<T> build)
        {
            try
            {
                return build();
            }
            catch (ArgumentException e)
            {
                throw fields.Rejected(Known(known, e.ParamName) ? fields.ItemPath(e.ParamName) : fields.Path, e.Message, e);
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

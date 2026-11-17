using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // TuningJson に並べず分けるのは、近接の調整値を読む側 (MeleeActionInstaller) だけがこの形を知ればよいようにするため。
    public static class MeleeJson
    {
        public const string DamageItem = "damage";
        public const string ReachItem = "reach";
        public const string ArcDegreesItem = "arcDegrees";
        public const string SwingSecondsItem = "swingSeconds";
        public const string CooldownSecondsItem = "cooldownSeconds";
        public const string ImpactSecondsItem = "impactSeconds";
        public const string LungeDistanceItem = "lungeDistance";
        public const string FinisherSecondsItem = "finisherSeconds";
        public const string FinisherHealthItem = "finisherHealth";
        public const string FinisherAmmoItem = "finisherAmmo";
        public const string FinisherInterruptibleItem = "finisherInterruptible";
        public const string TakedownSecondsItem = "takedownSeconds";
        public const string TakedownInterruptibleItem = "takedownInterruptible";
        public const string TakedownWitnessAwarenessItem = "takedownWitnessAwareness";
        public const string AlignSecondsItem = "alignSeconds";
        public const string StandDistanceItem = "standDistance";
        public const string BehindArcDegreesItem = "behindArcDegrees";

        private const string _kind = "melee JSON";

        // 項目名を MeleeSpec のコンストラクターの引数名にそろえ、値の検査で断られた項目を名前で示せるようにする。
        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            DamageItem,
            ReachItem,
            ArcDegreesItem,
            SwingSecondsItem,
            CooldownSecondsItem,
            ImpactSecondsItem,
            LungeDistanceItem,
            FinisherSecondsItem,
            FinisherHealthItem,
            FinisherAmmoItem,
            FinisherInterruptibleItem,
            TakedownSecondsItem,
            TakedownInterruptibleItem,
            TakedownWitnessAwarenessItem,
            AlignSecondsItem,
            StandDistanceItem,
            BehindArcDegreesItem,
        };

        public static MeleeSpec Parse(string text, string sourceName)
        {
            var source = TuningJson.SourceOf(sourceName, _kind);
            var fields = TuningJson.Fields.Read(text, source, _kind, ItemNames);
            var defaults = MeleeSpec.Default;
            try
            {
                return new MeleeSpec(
                    fields.ReadInt(DamageItem, defaults.Damage),
                    fields.ReadFloat(ReachItem, defaults.Reach),
                    fields.ReadFloat(ArcDegreesItem, defaults.ArcDegrees),
                    fields.ReadFloat(SwingSecondsItem, defaults.SwingSeconds),
                    fields.ReadFloat(CooldownSecondsItem, defaults.CooldownSeconds),
                    fields.ReadFloat(ImpactSecondsItem, defaults.ImpactSeconds),
                    fields.ReadFloat(LungeDistanceItem, defaults.LungeDistance),
                    fields.ReadFloat(FinisherSecondsItem, defaults.FinisherSeconds),
                    fields.ReadInt(FinisherHealthItem, defaults.FinisherHealth),
                    fields.ReadInt(FinisherAmmoItem, defaults.FinisherAmmo),
                    fields.ReadBool(FinisherInterruptibleItem, defaults.FinisherInterruptible),
                    fields.ReadFloat(TakedownSecondsItem, defaults.TakedownSeconds),
                    fields.ReadBool(TakedownInterruptibleItem, defaults.TakedownInterruptible),
                    fields.ReadFloat(TakedownWitnessAwarenessItem, defaults.TakedownWitnessAwareness),
                    fields.ReadFloat(AlignSecondsItem, defaults.AlignSeconds),
                    fields.ReadFloat(StandDistanceItem, defaults.StandDistance),
                    fields.ReadFloat(BehindArcDegreesItem, defaults.BehindArcDegrees));
            }
            catch (ArgumentException e)
            {
                throw TuningJson.Rejected(source, _kind, ItemNames, e);
            }
        }
    }
}

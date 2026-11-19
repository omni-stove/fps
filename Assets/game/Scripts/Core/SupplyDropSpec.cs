using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class SupplyDropSpec
    {
        // キットの loot.json には表が無いので、既定は表を引かず、シーンが渡す決まった中身だけを出す。
        // 名前を書いておくと、表が無いという警告を試合のたびに出してしまう。
        public const string DefaultTableId = "";

        public const float DefaultDropHeight = 40f;
        public const float DefaultFallSpeed = 6f;
        public const float DefaultPlaceFraction = 0.8f;
        public const float DefaultReach = 2.5f;
        public const float DefaultBotSearchRadius = 80f;

        public static IReadOnlyList<SupplyDropStage> DefaultStages { get; } = new[]
        {
            new SupplyDropStage(1, 5f, 10f),
            new SupplyDropStage(2, 5f, 10f),
            new SupplyDropStage(2, 5f, 8f),
        };

        public static SupplyDropSpec Default { get; } = new SupplyDropSpec();

        public SupplyDropSpec(
            IReadOnlyList<SupplyDropStage> stages = null,
            string tableId = DefaultTableId,
            float dropHeight = DefaultDropHeight,
            float fallSpeed = DefaultFallSpeed,
            float placeFraction = DefaultPlaceFraction,
            float reach = DefaultReach,
            float botSearchRadius = DefaultBotSearchRadius)
        {
            Stages = stages == null ? DefaultStages : CopyStages(stages);
            TableId = SpecGuard.ReferenceId(tableId);
            DropHeight = SpecGuard.PositiveFinite(dropHeight, nameof(dropHeight));
            FallSpeed = SpecGuard.PositiveFinite(fallSpeed, nameof(fallSpeed));
            PlaceFraction = SpecGuard.Fraction(placeFraction, nameof(placeFraction));
            Reach = SpecGuard.PositiveFinite(reach, nameof(reach));
            BotSearchRadius = SpecGuard.NonNegativeFinite(botSearchRadius, nameof(botSearchRadius));
        }

        // 並びの番号は縮むエリアの段階の番号。書いた数より段階が多ければ、最後の行をくり返す。
        public IReadOnlyList<SupplyDropStage> Stages { get; }

        public string TableId { get; }

        public bool UsesTable => TableId.Length > 0;

        public float DropHeight { get; }

        public float FallSpeed { get; }

        public float PlaceFraction { get; }

        public float Reach { get; }

        public float BotSearchRadius { get; }

        public SupplyDropStage StageFor(int phaseIndex)
        {
            if (phaseIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseIndex), phaseIndex, "phaseIndex must not be negative");
            }

            return Stages.Count == 0 ? null : Stages[Math.Min(phaseIndex, Stages.Count - 1)];
        }

        private static IReadOnlyList<SupplyDropStage> CopyStages(IReadOnlyList<SupplyDropStage> stages)
        {
            var copy = new SupplyDropStage[stages.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = stages[i] ?? throw new ArgumentException("stages must not contain null", nameof(stages));
            }

            return copy;
        }
    }

    public sealed class SupplyDropStage
    {
        public SupplyDropStage(int count, float delaySeconds, float intervalSeconds)
        {
            Count = SpecGuard.NonNegative(count, nameof(count));
            DelaySeconds = SpecGuard.NonNegativeFinite(delaySeconds, nameof(delaySeconds));
            IntervalSeconds = SpecGuard.NonNegativeFinite(intervalSeconds, nameof(intervalSeconds));
        }

        public int Count { get; }

        // 段階が始まってから最初の箱を落とすまで。
        public float DelaySeconds { get; }

        public float IntervalSeconds { get; }
    }
}

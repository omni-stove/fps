using System;

namespace Fps.Core
{
    public sealed class ContextMeleeMove
    {
        public const string FinisherId = "finisher";
        public const int FinisherPriority = 100;

        public const float DefaultAlignSeconds = 0.15f;

        // 体の中心どうしの距離。相手とプレイヤーの半径 (0.5m ずつ) より少し離し、位置合わせで体がめり込まないようにする。
        public const float DefaultStandDistance = 1.2f;

        /// <exception cref="ArgumentException">id が空か空白を含む、または required と excluded が重なる。</exception>
        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。outcome が Damage なら damage は 1 以上。</exception>
        public ContextMeleeMove(
            string id,
            int priority,
            ContextMeleeConditions required,
            ContextMeleeConditions excluded,
            float reach,
            float standDistance,
            float alignSeconds,
            float performSeconds,
            ContextMeleeOutcome outcome,
            int damage,
            int healthReward,
            int ammoReward,
            bool interruptible)
        {
            // ログを key=value で並べるので、空白を含む Id はほかの値と区切れなくなる。
            if (string.IsNullOrEmpty(id) || HasWhiteSpace(id))
            {
                throw new ArgumentException("id must be non-empty and contain no whitespace", nameof(id));
            }

            if ((required & excluded) != ContextMeleeConditions.None)
            {
                throw new ArgumentException($"required and excluded overlap ({required & excluded})", nameof(excluded));
            }

            if (!(reach > 0f) || float.IsInfinity(reach))
            {
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "reach must be positive and finite");
            }

            if (!(standDistance >= 0f) || float.IsInfinity(standDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(standDistance), standDistance, "standDistance must be a non-negative finite number");
            }

            if (!(alignSeconds >= 0f) || float.IsInfinity(alignSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(alignSeconds), alignSeconds, "alignSeconds must be a non-negative finite number");
            }

            if (!(performSeconds > 0f) || float.IsInfinity(performSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(performSeconds), performSeconds, "performSeconds must be positive and finite");
            }

            if (!Enum.IsDefined(typeof(ContextMeleeOutcome), outcome))
            {
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "outcome is not a defined ContextMeleeOutcome");
            }

            if (damage < 0 || (outcome == ContextMeleeOutcome.Damage && damage == 0))
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must not be negative, and must be positive for the Damage outcome");
            }

            if (healthReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(healthReward), healthReward, "healthReward must not be negative");
            }

            if (ammoReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ammoReward), ammoReward, "ammoReward must not be negative");
            }

            Id = id;
            Priority = priority;
            Required = required;
            Excluded = excluded;
            Reach = reach;
            StandDistance = standDistance;
            AlignSeconds = alignSeconds;
            PerformSeconds = performSeconds;
            Outcome = outcome;
            Damage = damage;
            HealthReward = healthReward;
            AmmoReward = ammoReward;
            Interruptible = interruptible;
        }

        public string Id { get; }

        public int Priority { get; }

        public ContextMeleeConditions Required { get; }

        public ContextMeleeConditions Excluded { get; }

        // ふつうの近接の届く距離と同じく、振る者の中心から相手の体の表面までで測る (MeleeSpec.Reach)。
        public float Reach { get; }

        public float StandDistance { get; }

        public float AlignSeconds { get; }

        public float PerformSeconds { get; }

        public ContextMeleeOutcome Outcome { get; }

        public int Damage { get; }

        public int HealthReward { get; }

        public int AmmoReward { get; }

        public bool Interruptible { get; }

        public float TotalSeconds => AlignSeconds + PerformSeconds;

        // 届く距離はふつうの近接と揃え、怯んだ相手に近接を押したときに「届いたのにふつうの近接が出た」とならないようにする。
        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public static ContextMeleeMove Finisher(MeleeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return new ContextMeleeMove(
                FinisherId,
                FinisherPriority,
                ContextMeleeConditions.TargetStaggered,
                ContextMeleeConditions.None,
                spec.Reach,
                DefaultStandDistance,
                DefaultAlignSeconds,
                spec.FinisherSeconds,
                ContextMeleeOutcome.Kill,
                0,
                spec.FinisherHealth,
                spec.FinisherAmmo,
                spec.FinisherInterruptible);
        }

        private static bool HasWhiteSpace(string value)
        {
            foreach (var c in value)
            {
                if (char.IsWhiteSpace(c))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

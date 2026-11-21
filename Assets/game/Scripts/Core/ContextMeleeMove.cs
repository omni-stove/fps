using System;

namespace Shooter.Core
{
    public sealed class ContextMeleeMove
    {
        public const string FinisherId = "finisher";
        public const int FinisherPriority = 100;

        public const string TakedownId = "takedown";

        // 怯んだ相手は撃たれて気づいた後なので、背後からでも報酬のある処刑技を先にする。
        public const int TakedownPriority = 50;

        public const string ExecuteId = "execute";

        // ダウン中の敵は Execution ではこの技でしか倒せないので、怯みや背後の条件が重なっても先に選ぶ。
        public const int ExecutePriority = 200;

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
            bool interruptible,
            float witnessAwareness = 0f)
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

            if (!(witnessAwareness >= 0f && witnessAwareness <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(witnessAwareness), witnessAwareness, "witnessAwareness must be between 0 and 1");
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
            WitnessAwareness = witnessAwareness;
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

        public float WitnessAwareness { get; }

        public float TotalSeconds => AlignSeconds + PerformSeconds;

        // 届く距離はふつうの近接と揃え、怯んだ相手に近接を押したときに「届いたのにふつうの近接が出た」とならないようにする。
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
                spec.StandDistance,
                spec.AlignSeconds,
                spec.FinisherSeconds,
                ContextMeleeOutcome.Kill,
                0,
                spec.FinisherHealth,
                spec.FinisherAmmo,
                spec.FinisherInterruptible);
        }

        // 気づいていない相手を倒すので報酬は付けない。
        public static ContextMeleeMove Takedown(MeleeSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return new ContextMeleeMove(
                TakedownId,
                TakedownPriority,
                ContextMeleeConditions.TargetBehind | ContextMeleeConditions.TargetUnaware,
                ContextMeleeConditions.None,
                spec.Reach,
                spec.StandDistance,
                spec.AlignSeconds,
                spec.TakedownSeconds,
                ContextMeleeOutcome.Kill,
                0,
                0,
                0,
                spec.TakedownInterruptible,
                spec.TakedownWitnessAwareness);
        }

        // オフラインの ExecutionSession と同じく、とどめの間に撃たれても止めず、報酬も付けない。
        // 足元どうしでなく中心から体の表面までで測るのは、テイクダウンと同じ探し方 (近接の扇) で相手を選ぶため。
        public static ContextMeleeMove Execute(ExecutionSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return new ContextMeleeMove(
                ExecuteId,
                ExecutePriority,
                ContextMeleeConditions.TargetDowned,
                ContextMeleeConditions.None,
                spec.ExecuteRange,
                0f,
                0f,
                spec.ExecuteSeconds,
                ContextMeleeOutcome.Kill,
                0,
                0,
                0,
                false);
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

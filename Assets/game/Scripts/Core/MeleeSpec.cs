using System;

namespace Fps.Core
{
    public sealed class MeleeSpec
    {
        public const int DefaultDamage = 50;
        public const float DefaultReach = 2f;
        public const float DefaultArcDegrees = 90f;
        public const float DefaultSwingSeconds = 0.5f;
        public const float DefaultImpactSeconds = 0.2f;
        public const float DefaultCooldownSeconds = 0.4f;
        public const float DefaultLungeDistance = 0.75f;
        public const float DefaultFinisherSeconds = 1.2f;
        public const int DefaultFinisherHealth = 25;
        public const int DefaultFinisherAmmo = 30;
        public const bool DefaultFinisherInterruptible = true;

        public const float MaxArcDegrees = 360f;

        public static readonly MeleeSpec Default = new MeleeSpec(
            DefaultDamage,
            DefaultReach,
            DefaultArcDegrees,
            DefaultSwingSeconds,
            DefaultCooldownSeconds,
            DefaultImpactSeconds,
            DefaultLungeDistance);

        public int Damage { get; }

        // 振る者の中心から相手の体の表面までの距離 (m) で測る。中心までにすると太い相手ほど届かなくなる。
        public float Reach { get; }

        // 半角でなく全体の角度で持つのは、JSON や Inspector で「前方 90 度」と書くほうが読み違えにくいから。
        public float ArcDegrees { get; }

        public float SwingSeconds { get; }

        // 振り終えてから数える。振り始めから数えると SwingSeconds より短い値が意味を持たず、組み合わせの検査が要る。
        public float CooldownSeconds { get; }

        // 踏み込みはこの瞬間までに終え、当たりはこの瞬間に 1 度だけ判定する。踏み込んだ後の位置で測るため。
        public float ImpactSeconds { get; }

        public float LungeDistance { get; }

        public float HalfArcDegrees => ArcDegrees / 2f;

        public float FinisherSeconds { get; }

        public int FinisherHealth { get; }

        public int FinisherAmmo { get; }

        public bool FinisherInterruptible { get; }

        // 処刑技 (ContextMeleeMove.Finisher) の値を省けるのは、敵の近接や盾の殴りなど処刑技を出さない側が値を並べなくて済むようにするため。
        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。impactSeconds は 0 以上 swingSeconds 以下。</exception>
        public MeleeSpec(
            int damage,
            float reach,
            float arcDegrees,
            float swingSeconds,
            float cooldownSeconds,
            float impactSeconds,
            float lungeDistance,
            float finisherSeconds = DefaultFinisherSeconds,
            int finisherHealth = DefaultFinisherHealth,
            int finisherAmmo = DefaultFinisherAmmo,
            bool finisherInterruptible = DefaultFinisherInterruptible)
        {
            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, "damage must be positive");
            }

            if (!(reach > 0f) || float.IsInfinity(reach))
            {
                throw new ArgumentOutOfRangeException(nameof(reach), reach, "reach must be positive and finite");
            }

            if (!(arcDegrees > 0f && arcDegrees <= MaxArcDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(arcDegrees), arcDegrees, "arcDegrees must be greater than 0 and at most 360");
            }

            if (!(swingSeconds > 0f) || float.IsInfinity(swingSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(swingSeconds), swingSeconds, "swingSeconds must be positive and finite");
            }

            if (!(cooldownSeconds >= 0f) || float.IsInfinity(cooldownSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(cooldownSeconds), cooldownSeconds, "cooldownSeconds must be a non-negative finite number");
            }

            if (!(impactSeconds >= 0f && impactSeconds <= swingSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(impactSeconds), impactSeconds, "impactSeconds must be between 0 and swingSeconds");
            }

            if (!(lungeDistance >= 0f) || float.IsInfinity(lungeDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(lungeDistance), lungeDistance, "lungeDistance must be a non-negative finite number");
            }

            if (!(finisherSeconds > 0f) || float.IsInfinity(finisherSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(finisherSeconds), finisherSeconds, "finisherSeconds must be positive and finite");
            }

            if (finisherHealth < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(finisherHealth), finisherHealth, "finisherHealth must not be negative");
            }

            if (finisherAmmo < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(finisherAmmo), finisherAmmo, "finisherAmmo must not be negative");
            }

            Damage = damage;
            Reach = reach;
            ArcDegrees = arcDegrees;
            SwingSeconds = swingSeconds;
            CooldownSeconds = cooldownSeconds;
            ImpactSeconds = impactSeconds;
            LungeDistance = lungeDistance;
            FinisherSeconds = finisherSeconds;
            FinisherHealth = finisherHealth;
            FinisherAmmo = finisherAmmo;
            FinisherInterruptible = finisherInterruptible;
        }

        // 踏み込みの間は一定の速さで進む。当たりの瞬間が 0 秒なら踏み込まない (0 で割らない)。
        public float LungeSpeed => ImpactSeconds > 0f ? LungeDistance / ImpactSeconds : 0f;
    }
}

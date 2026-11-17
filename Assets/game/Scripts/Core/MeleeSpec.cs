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
        public const float DefaultTakedownSeconds = 1.5f;
        public const bool DefaultTakedownInterruptible = true;
        public const float DefaultTakedownWitnessAwareness = 1f;
        public const float DefaultAlignSeconds = 0.15f;

        // 体の中心どうしの距離。相手とプレイヤーの半径 (0.5m ずつ) より少し離し、位置合わせで体がめり込まないようにする。
        public const float DefaultStandDistance = 1.2f;

        public const float DefaultBehindArcDegrees = ContextMeleeSelector.DefaultBehindArcDegrees;

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

        public float TakedownSeconds { get; }

        public bool TakedownInterruptible { get; }

        public float TakedownWitnessAwareness { get; }

        // 処刑技と背後から倒す技で同じ値を使う。技ごとに分けると、どちらかを変え忘れたときに構えの見た目が技で揃わなくなる。
        public float AlignSeconds { get; }

        public float StandDistance { get; }

        // 相手の真後ろを中心にした扇の全体の角度。ArcDegrees と同じく半角でなく全体で持つ。
        public float BehindArcDegrees { get; }

        // 処刑技 (ContextMeleeMove.Finisher) と背後から倒す技 (ContextMeleeMove.Takedown) の値を省けるのは、敵の近接や盾の殴りなど技を出さない側が値を並べなくて済むようにするため。
        /// <exception cref="ArgumentOutOfRangeException">値が範囲の外。impactSeconds は 0 以上 swingSeconds 以下、takedownWitnessAwareness は 0 以上 1 以下、behindArcDegrees は 1 以上 360 以下。</exception>
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
            bool finisherInterruptible = DefaultFinisherInterruptible,
            float takedownSeconds = DefaultTakedownSeconds,
            bool takedownInterruptible = DefaultTakedownInterruptible,
            float takedownWitnessAwareness = DefaultTakedownWitnessAwareness,
            float alignSeconds = DefaultAlignSeconds,
            float standDistance = DefaultStandDistance,
            float behindArcDegrees = DefaultBehindArcDegrees)
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

            if (!(takedownSeconds > 0f) || float.IsInfinity(takedownSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(takedownSeconds), takedownSeconds, "takedownSeconds must be positive and finite");
            }

            if (!(takedownWitnessAwareness >= 0f && takedownWitnessAwareness <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(takedownWitnessAwareness), takedownWitnessAwareness, "takedownWitnessAwareness must be between 0 and 1");
            }

            if (!(alignSeconds >= 0f) || float.IsInfinity(alignSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(alignSeconds), alignSeconds, "alignSeconds must be a non-negative finite number");
            }

            if (!(standDistance >= 0f) || float.IsInfinity(standDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(standDistance), standDistance, "standDistance must be a non-negative finite number");
            }

            if (!(behindArcDegrees >= 1f && behindArcDegrees <= MaxArcDegrees))
            {
                throw new ArgumentOutOfRangeException(nameof(behindArcDegrees), behindArcDegrees, "behindArcDegrees must be at least 1 and at most 360");
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
            TakedownSeconds = takedownSeconds;
            TakedownInterruptible = takedownInterruptible;
            TakedownWitnessAwareness = takedownWitnessAwareness;
            AlignSeconds = alignSeconds;
            StandDistance = standDistance;
            BehindArcDegrees = behindArcDegrees;
        }

        // 踏み込みの間は一定の速さで進む。当たりの瞬間が 0 秒なら踏み込まない (0 で割らない)。
        public float LungeSpeed => ImpactSeconds > 0f ? LungeDistance / ImpactSeconds : 0f;
    }
}

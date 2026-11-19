using System;

namespace Shooter.Core
{
    public sealed class MeleeWeaponSpec
    {
        public const int DefaultSwingDamage = 35;
        public const float DefaultSwingReach = 2f;
        public const float DefaultSwingArcDegrees = 80f;
        public const float DefaultSwingSeconds = 0.4f;
        public const float DefaultSwingCooldownSeconds = 0.1f;
        public const float DefaultSwingImpactSeconds = 0.15f;
        public const float DefaultSwingLungeDistance = 0.3f;

        // これより短く押して離したら、溜めずにふつうに振る。クリックのたびに少しだけ溜まった振りにならないように。
        public const float DefaultChargeDelaySeconds = 0.2f;

        public const float DefaultChargeSeconds = 0.8f;

        public static readonly MeleeSpec DefaultSwing = new MeleeSpec(
            DefaultSwingDamage,
            DefaultSwingReach,
            DefaultSwingArcDegrees,
            DefaultSwingSeconds,
            DefaultSwingCooldownSeconds,
            DefaultSwingImpactSeconds,
            DefaultSwingLungeDistance);

        public static readonly MeleeWeaponSpec Default = new MeleeWeaponSpec();

        public MeleeWeaponSpec(
            MeleeSpec swing = null,
            MeleeSpec heavySwing = null,
            float chargeDelaySeconds = DefaultChargeDelaySeconds,
            float chargeSeconds = DefaultChargeSeconds,
            MeleeThrowSpec @throw = null)
        {
            Swing = swing ?? DefaultSwing;
            HeavySwing = heavySwing;
            ChargeDelaySeconds = SpecGuard.NonNegativeFinite(chargeDelaySeconds, nameof(chargeDelaySeconds));
            ChargeSeconds = SpecGuard.PositiveFinite(chargeSeconds, nameof(chargeSeconds));
            Throw = @throw;
        }

        public MeleeSpec Swing { get; }

        public MeleeSpec HeavySwing { get; }

        public float ChargeDelaySeconds { get; }

        public float ChargeSeconds { get; }

        public MeleeThrowSpec Throw { get; }

        public bool CanCharge => HeavySwing != null;

        public bool CanThrow => Throw != null;

        public float ChargeFraction(float heldSeconds)
        {
            if (!CanCharge || !(heldSeconds > ChargeDelaySeconds))
            {
                return 0f;
            }

            return Math.Min(1f, (heldSeconds - ChargeDelaySeconds) / ChargeSeconds);
        }

        // 溜めた分だけ、ふつうの振りから溜め切った振りへ全部の値を寄せる。途中で離しても溜めた分の強さが出るように。
        public MeleeSpec SwingFor(float chargeFraction)
        {
            if (!CanCharge || !(chargeFraction > 0f))
            {
                return Swing;
            }

            if (chargeFraction >= 1f)
            {
                return HeavySwing;
            }

            var light = Swing;
            var heavy = HeavySwing;
            var t = chargeFraction;
            return new MeleeSpec(
                (int)Math.Round(Lerp(light.Damage, heavy.Damage, t), MidpointRounding.AwayFromZero),
                Lerp(light.Reach, heavy.Reach, t),
                Lerp(light.ArcDegrees, heavy.ArcDegrees, t),
                Lerp(light.SwingSeconds, heavy.SwingSeconds, t),
                Lerp(light.CooldownSeconds, heavy.CooldownSeconds, t),
                Math.Min(Lerp(light.ImpactSeconds, heavy.ImpactSeconds, t), Lerp(light.SwingSeconds, heavy.SwingSeconds, t)),
                Lerp(light.LungeDistance, heavy.LungeDistance, t));
        }

        private static float Lerp(float from, float to, float t)
        {
            return from + ((to - from) * t);
        }
    }
}

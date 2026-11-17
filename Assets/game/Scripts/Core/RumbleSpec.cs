using System;

namespace Shooter.Core
{
    public readonly struct RumblePulse : IEquatable<RumblePulse>
    {
        public static RumblePulse None => default;

        public float Low { get; }

        public float High { get; }

        public float Seconds { get; }

        public bool IsSilent => Seconds <= 0f || (Low <= 0f && High <= 0f);

        public RumblePulse(float low, float high, float seconds)
        {
            Low = SpecGuard.Fraction(low, nameof(low));
            High = SpecGuard.Fraction(high, nameof(high));
            Seconds = SpecGuard.NonNegativeFinite(seconds, nameof(seconds));
        }

        public bool Equals(RumblePulse other)
        {
            return Low.Equals(other.Low) && High.Equals(other.High) && Seconds.Equals(other.Seconds);
        }

        public override bool Equals(object obj)
        {
            return obj is RumblePulse other && Equals(other);
        }

        public static bool operator ==(RumblePulse left, RumblePulse right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RumblePulse left, RumblePulse right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Low.GetHashCode();
                hash = (hash * 397) ^ High.GetHashCode();
                return (hash * 397) ^ Seconds.GetHashCode();
            }
        }
    }

    public sealed class RumbleSpec
    {
        public const float DefaultFireLow = 0.15f;
        public const float DefaultFireHigh = 0.35f;
        public const float DefaultFireSeconds = 0.08f;
        public const float DefaultDamageLow = 0.6f;
        public const float DefaultDamageHigh = 0.3f;
        public const float DefaultDamageSeconds = 0.25f;
        public const float DefaultExplosionLow = 1f;
        public const float DefaultExplosionHigh = 0.6f;
        public const float DefaultExplosionSeconds = 0.45f;
        public const float DefaultExplosionRadius = 25f;

        public static RumbleSpec Default { get; } = new RumbleSpec();

        public RumblePulse Fire { get; }

        public RumblePulse Damage { get; }

        public RumblePulse Explosion { get; }

        public float ExplosionRadius { get; }

        public RumbleSpec(
            float fireLow = DefaultFireLow,
            float fireHigh = DefaultFireHigh,
            float fireSeconds = DefaultFireSeconds,
            float damageLow = DefaultDamageLow,
            float damageHigh = DefaultDamageHigh,
            float damageSeconds = DefaultDamageSeconds,
            float explosionLow = DefaultExplosionLow,
            float explosionHigh = DefaultExplosionHigh,
            float explosionSeconds = DefaultExplosionSeconds,
            float explosionRadius = DefaultExplosionRadius)
        {
            Fire = Pulse(fireLow, nameof(fireLow), fireHigh, nameof(fireHigh), fireSeconds, nameof(fireSeconds));
            Damage = Pulse(damageLow, nameof(damageLow), damageHigh, nameof(damageHigh), damageSeconds, nameof(damageSeconds));
            Explosion = Pulse(explosionLow, nameof(explosionLow), explosionHigh, nameof(explosionHigh), explosionSeconds, nameof(explosionSeconds));
            ExplosionRadius = SpecGuard.PositiveFinite(explosionRadius, nameof(explosionRadius));
        }

        // RumblePulse の引数名 (low・high・seconds) ではどの出来事の値か分からないので、先にこちらの名前で確かめる。
        private static RumblePulse Pulse(float low, string lowName, float high, string highName, float seconds, string secondsName)
        {
            return new RumblePulse(
                SpecGuard.Fraction(low, lowName),
                SpecGuard.Fraction(high, highName),
                SpecGuard.NonNegativeFinite(seconds, secondsName));
        }
    }
}

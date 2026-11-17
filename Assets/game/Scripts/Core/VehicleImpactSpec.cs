using System;

namespace Shooter.Core
{
    public sealed class VehicleImpactSpec
    {
        public const float DefaultMinSpeed = 4f;
        public const float DefaultDamagePerSpeed = 10f;
        public const int DefaultMaxDamage = 200;
        public const float DefaultRepeatSeconds = 1f;

        public VehicleImpactSpec(
            float minSpeed = DefaultMinSpeed,
            float damagePerSpeed = DefaultDamagePerSpeed,
            int maxDamage = DefaultMaxDamage,
            float repeatSeconds = DefaultRepeatSeconds)
        {
            if (!(minSpeed >= 0f) || float.IsInfinity(minSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(minSpeed), minSpeed, "minSpeed must be a non-negative finite number");
            }

            if (!(damagePerSpeed >= 0f) || float.IsInfinity(damagePerSpeed))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damagePerSpeed), damagePerSpeed, "damagePerSpeed must be a non-negative finite number");
            }

            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            if (!(repeatSeconds >= 0f) || float.IsInfinity(repeatSeconds))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(repeatSeconds), repeatSeconds, "repeatSeconds must be a non-negative finite number");
            }

            MinSpeed = minSpeed;
            DamagePerSpeed = damagePerSpeed;
            MaxDamage = maxDamage;
            RepeatSeconds = repeatSeconds;
        }

        public static VehicleImpactSpec Default => new VehicleImpactSpec();

        public float MinSpeed { get; }

        public float DamagePerSpeed { get; }

        public int MaxDamage { get; }

        public float RepeatSeconds { get; }

        public int DamageAt(float speed)
        {
            if (!(speed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative or NaN");
            }

            if (speed <= MinSpeed)
            {
                return 0;
            }

            if (float.IsPositiveInfinity(speed))
            {
                return DamagePerSpeed > 0f ? MaxDamage : 0;
            }

            // 切り捨てにするのは、MinSpeed をわずかに超えただけの接触で 1 ずつ削れ続けないようにするため。
            var damage = Math.Floor(((double)speed - MinSpeed) * DamagePerSpeed);
            return damage >= MaxDamage ? MaxDamage : (int)damage;
        }
    }
}

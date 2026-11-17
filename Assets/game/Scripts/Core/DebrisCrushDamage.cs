using System;

namespace Shooter.Core
{
    // 速さから MinSpeed を引くのは、床に転がっている破片や飛び散る小さな破片が触れただけで体力を減らさないため。
    public sealed class DebrisCrushDamage
    {
        public const float DefaultMinSpeed = 2f;
        public const float DefaultDamagePerMomentum = 0.25f;
        public const int DefaultMaxDamage = 200;

        public DebrisCrushDamage(float minSpeed, float damagePerMomentum, int maxDamage)
        {
            if (!(minSpeed >= 0f) || float.IsInfinity(minSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(minSpeed), minSpeed, "minSpeed must be a non-negative finite number");
            }

            if (!(damagePerMomentum >= 0f) || float.IsInfinity(damagePerMomentum))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(damagePerMomentum), damagePerMomentum, "damagePerMomentum must be a non-negative finite number");
            }

            if (maxDamage < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDamage), maxDamage, "maxDamage must not be negative");
            }

            MinSpeed = minSpeed;
            DamagePerMomentum = damagePerMomentum;
            MaxDamage = maxDamage;
        }

        public static DebrisCrushDamage Default => new DebrisCrushDamage(DefaultMinSpeed, DefaultDamagePerMomentum, DefaultMaxDamage);

        public float MinSpeed { get; }

        public float DamagePerMomentum { get; }

        public int MaxDamage { get; }

        public int At(float speed, float mass)
        {
            if (!(speed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative or NaN");
            }

            if (!(mass > 0f) || float.IsInfinity(mass))
            {
                throw new ArgumentOutOfRangeException(nameof(mass), mass, "mass must be positive and finite");
            }

            if (speed <= MinSpeed)
            {
                return 0;
            }

            if (float.IsPositiveInfinity(speed))
            {
                return DamagePerMomentum > 0f ? MaxDamage : 0;
            }

            // 切り捨てにするのは、MinSpeed をわずかに超えただけの接触で 1 ずつ削れ続けないようにするため。
            var damage = Math.Floor(((double)speed - MinSpeed) * mass * DamagePerMomentum);
            return damage >= MaxDamage ? MaxDamage : (int)damage;
        }
    }
}

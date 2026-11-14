using System;
using System.Numerics;

namespace Fps.Core
{
    public sealed class KnockbackSpec
    {
        public const float DefaultWeight = 1f;

        public const float DefaultExplosionSpeed = 8f;
        public const float DefaultExplosionUpwardRatio = 0.45f;
        public const float DefaultExplosionWeightFactor = 1f;

        public const float DefaultMeleeSpeed = 4f;
        public const float DefaultMeleeUpwardRatio = 0.2f;
        public const float DefaultMeleeWeightFactor = 1f;

        public const float DefaultChargeSpeed = 7f;
        public const float DefaultChargeUpwardRatio = 0.35f;
        public const float DefaultChargeWeightFactor = 1f;

        // 爆発の端のように少ししか押さない力でも飛ばすと、立ち止まって小さく跳ねるだけで操作が切れる。
        public const float DefaultMinSpeed = 1.5f;

        // 飛ばした直後の Tick ではまだ床に触れているので、すぐには着地を見ない。
        public const float DefaultMinAirSeconds = 0.15f;

        public const float DefaultMaxAirSeconds = 3f;
        public const float DefaultAirControl = 1f;
        public const float DefaultGravity = 20f;
        public const float DefaultRecoverSeconds = 0.4f;
        public const float DefaultLandingSnapRadius = 4f;

        public static readonly KnockbackSpec Default = new KnockbackSpec();

        public KnockbackSpec(
            KnockbackForce? explosion = null,
            KnockbackForce? melee = null,
            KnockbackForce? charge = null,
            float minSpeed = DefaultMinSpeed,
            float minAirSeconds = DefaultMinAirSeconds,
            float maxAirSeconds = DefaultMaxAirSeconds,
            float airControl = DefaultAirControl,
            float gravity = DefaultGravity,
            float recoverSeconds = DefaultRecoverSeconds,
            float landingSnapRadius = DefaultLandingSnapRadius)
        {
            RequireNonNegativeFinite(minSpeed, nameof(minSpeed));
            RequireNonNegativeFinite(minAirSeconds, nameof(minAirSeconds));
            RequirePositiveFinite(maxAirSeconds, nameof(maxAirSeconds));
            RequireNonNegativeFinite(airControl, nameof(airControl));
            RequirePositiveFinite(gravity, nameof(gravity));
            RequirePositiveFinite(recoverSeconds, nameof(recoverSeconds));
            RequirePositiveFinite(landingSnapRadius, nameof(landingSnapRadius));

            // 着地を見始める前に打ち切ると、平らな床でも必ず打ち切りになる。
            if (minAirSeconds >= maxAirSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(minAirSeconds), minAirSeconds, "minAirSeconds must be less than maxAirSeconds");
            }

            Explosion = explosion ?? new KnockbackForce(DefaultExplosionSpeed, DefaultExplosionUpwardRatio, DefaultExplosionWeightFactor);
            Melee = melee ?? new KnockbackForce(DefaultMeleeSpeed, DefaultMeleeUpwardRatio, DefaultMeleeWeightFactor);
            Charge = charge ?? new KnockbackForce(DefaultChargeSpeed, DefaultChargeUpwardRatio, DefaultChargeWeightFactor);
            MinSpeed = minSpeed;
            MinAirSeconds = minAirSeconds;
            MaxAirSeconds = maxAirSeconds;
            AirControl = airControl;
            Gravity = gravity;
            RecoverSeconds = recoverSeconds;
            LandingSnapRadius = landingSnapRadius;
        }

        public KnockbackForce Explosion { get; }

        public KnockbackForce Melee { get; }

        public KnockbackForce Charge { get; }

        public float MinSpeed { get; }

        public float MinAirSeconds { get; }

        public float MaxAirSeconds { get; }

        public float AirControl { get; }

        public float Gravity { get; }

        public float RecoverSeconds { get; }

        public float LandingSnapRadius { get; }

        // 銃弾は 1 発ごとに飛ばすと連射で浮き続けるので、種類の無いダメージ (落下など) と同じく飛ばさない。
        public KnockbackForce For(DamageKind kind)
        {
            switch (kind)
            {
                case DamageKind.Explosion:
                    return Explosion;
                case DamageKind.Melee:
                    return Melee;
                case DamageKind.Charge:
                    return Charge;
                default:
                    return KnockbackForce.None;
            }
        }

        // 爆発は中心からの距離で強さが変わり、被弾の知らせには強さが無いので、爆発させた側が強さを付けて飛ばす。
        // 近接と突進は 1 回の当たりの力がいつも同じなので、知らせの向きだけで飛ばせる。
        public static bool ComesFromReport(DamageKind kind)
        {
            return kind == DamageKind.Melee || kind == DamageKind.Charge;
        }

        // 真上・真下から押されて水平の向きが無いときは、上向きの割合があれば真上へ飛ばす。床へめり込む向きには飛ばさない。
        public bool TryGetVelocity(DamageKind kind, Vector3 direction, float strength, float weight, out Vector3 velocity)
        {
            if (!IsFinite(direction))
            {
                throw new ArgumentException($"direction must be finite: {direction}", nameof(direction));
            }

            if (!(strength >= 0f && strength <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "strength must be within [0, 1]");
            }

            RequirePositiveFinite(weight, nameof(weight));

            velocity = Vector3.Zero;
            var force = For(kind);
            var speed = force.Speed * strength / MathF.Pow(weight, force.WeightFactor);
            if (!(speed > 0f) || speed < MinSpeed || float.IsInfinity(speed))
            {
                return false;
            }

            var flat = new Vector3(direction.X, 0f, direction.Z);
            Vector3 heading;
            if (flat.LengthSquared() > 1e-8f)
            {
                var up = force.UpwardRatio;
                heading = Vector3.Normalize((Vector3.Normalize(flat) * (1f - up)) + (Vector3.UnitY * up));
            }
            else if (force.UpwardRatio > 0f)
            {
                heading = Vector3.UnitY;
            }
            else
            {
                return false;
            }

            velocity = heading * speed;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static void RequireNonNegativeFinite(float value, string name)
        {
            if (!(value >= 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a non-negative finite number");
            }
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be a positive finite number");
            }
        }
    }
}

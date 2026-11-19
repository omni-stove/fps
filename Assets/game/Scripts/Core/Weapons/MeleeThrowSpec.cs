using System;

namespace Shooter.Core
{
    public sealed class MeleeThrowSpec
    {
        public const int DefaultDamage = 60;
        public const float DefaultSpeed = 18f;
        public const float DefaultUpwardDegrees = 4f;
        public const float DefaultSpinDegreesPerSecond = 720f;
        public const float DefaultRadius = 0.12f;

        // 跳ね続けたり奈落へ落ち続けたりしても、拾える物に変わらないまま残らないようにする。
        public const float DefaultMaxFlightSeconds = 8f;

        public static readonly MeleeThrowSpec Default = new MeleeThrowSpec();

        public MeleeThrowSpec(
            int damage = DefaultDamage,
            float speed = DefaultSpeed,
            float upwardDegrees = DefaultUpwardDegrees,
            float spinDegreesPerSecond = DefaultSpinDegreesPerSecond,
            float radius = DefaultRadius,
            float maxFlightSeconds = DefaultMaxFlightSeconds)
        {
            if (damage <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(damage), damage, nameof(damage) + " must be positive");
            }

            Damage = damage;
            Speed = SpecGuard.PositiveFinite(speed, nameof(speed));
            UpwardDegrees = SpecGuard.Angle(upwardDegrees, nameof(upwardDegrees));
            SpinDegreesPerSecond = SpecGuard.NonNegativeFinite(spinDegreesPerSecond, nameof(spinDegreesPerSecond));
            Radius = SpecGuard.PositiveFinite(radius, nameof(radius));
            MaxFlightSeconds = SpecGuard.PositiveFinite(maxFlightSeconds, nameof(maxFlightSeconds));
        }

        // 最初に当たった 1 人にだけ与える。跳ね返った後にもう一度当てると、1 回の投げで何人も倒せてしまう。
        public int Damage { get; }

        public float Speed { get; }

        // 照準の向きから上へ起こす角度。重さで落ちる分を、狙った所の少し上へ投げて補う。
        public float UpwardDegrees { get; }

        public float SpinDegreesPerSecond { get; }

        public float Radius { get; }

        public float MaxFlightSeconds { get; }
    }
}

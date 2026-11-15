using System;

namespace Fps.Core
{
    public sealed class RoundSpec
    {
        public const float DefaultRange = 100f;

        public static readonly RoundSpec Default = new RoundSpec();

        public RoundSpec(
            RoundDelivery delivery = RoundDelivery.Hitscan,
            float range = DefaultRange,
            int pelletCount = 1,
            float pelletSpreadDegrees = 0f,
            float radius = 0f,
            PenetrationSpec penetration = null,
            RicochetSpec ricochet = null,
            ProjectileSpec projectile = null,
            HomingSpec homing = null,
            BeamSpec beam = null,
            SpraySpec spray = null)
        {
            SpecGuard.Defined(delivery, nameof(delivery));
            SpecGuard.PositiveFinite(range, nameof(range));
            SpecGuard.AtLeast(pelletCount, 1, nameof(pelletCount));
            SpecGuard.Angle(pelletSpreadDegrees, nameof(pelletSpreadDegrees));
            SpecGuard.NonNegativeFinite(radius, nameof(radius));

            // 届け方に合わない入れ子は使われずに残り、設定したのに効かない値になるので、作るときに弾く。
            RequireExactlyWhen(projectile, delivery == RoundDelivery.Projectile, nameof(projectile), RoundDelivery.Projectile);
            RequireExactlyWhen(beam, delivery == RoundDelivery.Beam, nameof(beam), RoundDelivery.Beam);
            RequireExactlyWhen(spray, delivery == RoundDelivery.Spray, nameof(spray), RoundDelivery.Spray);

            if (homing != null && delivery != RoundDelivery.Projectile)
            {
                throw new ArgumentException("homing needs delivery Projectile", nameof(homing));
            }

            Delivery = delivery;
            Range = range;
            PelletCount = pelletCount;
            PelletSpreadDegrees = pelletSpreadDegrees;
            Radius = radius;
            Penetration = penetration;
            Ricochet = ricochet;
            Projectile = projectile;
            Homing = homing;
            Beam = beam;
            Spray = spray;
        }

        public RoundDelivery Delivery { get; }

        public float Range { get; }

        public int PelletCount { get; }

        public float PelletSpreadDegrees { get; }

        public float Radius { get; }

        public PenetrationSpec Penetration { get; }

        public RicochetSpec Ricochet { get; }

        public ProjectileSpec Projectile { get; }

        public HomingSpec Homing { get; }

        public BeamSpec Beam { get; }

        public SpraySpec Spray { get; }

        private static void RequireExactlyWhen(object part, bool required, string paramName, RoundDelivery delivery)
        {
            if (required && part == null)
            {
                throw new ArgumentNullException(paramName, paramName + " is required when delivery is " + delivery);
            }

            if (!required && part != null)
            {
                throw new ArgumentException(paramName + " must be null unless delivery is " + delivery, paramName);
            }
        }
    }
}

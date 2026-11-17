using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ShotDeliveries
    {
        public static IShotDelivery For(RoundDelivery delivery)
        {
            switch (delivery)
            {
                case RoundDelivery.Hitscan:
                    return HitscanDelivery.Instance;
                case RoundDelivery.Projectile:
                    return ProjectileDelivery.Instance;
                case RoundDelivery.Beam:
                    return BeamDelivery.Instance;
                case RoundDelivery.Spray:
                    return SprayDelivery.Instance;
                default:
                    throw new ArgumentOutOfRangeException(nameof(delivery), delivery, "delivery is not a defined RoundDelivery");
            }
        }

        public static ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            return For(request.Round.Delivery).Deliver(request, round);
        }
    }
}

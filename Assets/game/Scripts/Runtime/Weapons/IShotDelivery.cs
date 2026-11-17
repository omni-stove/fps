using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IShotDelivery
    {
        ShotOutcome Deliver(ShotRequest request, PlannedRound round);
    }
}

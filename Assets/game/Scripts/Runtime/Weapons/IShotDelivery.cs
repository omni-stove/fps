using Fps.Core;

namespace Fps.Runtime
{
    public interface IShotDelivery
    {
        ShotOutcome Deliver(ShotRequest request, PlannedRound round);
    }
}

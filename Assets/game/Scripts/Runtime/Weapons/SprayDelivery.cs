using Fps.Core;

namespace Fps.Runtime
{
    public sealed class SprayDelivery : IShotDelivery
    {
        public static readonly SprayDelivery Instance = new SprayDelivery();

        // 専用の届け方が入るまでは、この届け方の武器も撃てるようヒットスキャンで届ける。
        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            return HitscanDelivery.Instance.Deliver(request, round);
        }
    }
}

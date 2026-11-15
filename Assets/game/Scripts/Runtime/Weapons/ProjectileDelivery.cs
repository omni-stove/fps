using Fps.Core;

namespace Fps.Runtime
{
    public sealed class ProjectileDelivery : IShotDelivery
    {
        public static readonly ProjectileDelivery Instance = new ProjectileDelivery();

        // 専用の届け方が入るまでは、この届け方の武器も撃てるようヒットスキャンで届ける。
        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            return HitscanDelivery.Instance.Deliver(request, round);
        }
    }
}

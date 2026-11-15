using Fps.Core;

namespace Fps.Server
{
    internal sealed partial class ServerImpact
    {
        partial void Afflict(in ServerShot shot, in ShotHit hit, ImpactOrder order)
        {
            if (hit.HitTarget)
            {
                _combat.StatusEffects.Afflict(shot, hit.TargetId, order.StatusEffect);
            }
        }
    }
}

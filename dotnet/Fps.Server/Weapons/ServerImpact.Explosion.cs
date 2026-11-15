using Fps.Core;

namespace Fps.Server
{
    internal sealed partial class ServerImpact
    {
        partial void Explode(in ServerShot shot, in ShotHit hit, ImpactOrder order)
        {
            var center = ImpactResolver.DetonationPoint(order.Point, hit.Normal);
            _combat.Explode(center, order.Explosion, shot.ShooterId, shot.Weapon.Id);
        }
    }
}

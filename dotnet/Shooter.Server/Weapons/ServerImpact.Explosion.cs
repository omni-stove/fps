using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerImpact
    {
        public void Detonate(in ServerShot shot, DetonationTrigger cause, Vector3 point, Vector3 normal)
        {
            foreach (var order in ImpactResolver.ResolveDetonation(shot.Impact, cause, point))
            {
                var center = ImpactResolver.DetonationPoint(order.Point, normal);
                _combat.Explode(center, order.Explosion, shot.ShooterId, shot.Weapon.Id);
            }
        }

        partial void Explode(in ServerShot shot, in ShotHit hit, ImpactOrder order)
        {
            var center = ImpactResolver.DetonationPoint(order.Point, hit.Normal);
            _combat.Explode(center, order.Explosion, shot.ShooterId, shot.Weapon.Id);
        }
    }
}

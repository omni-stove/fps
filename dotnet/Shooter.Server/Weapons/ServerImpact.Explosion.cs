using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerImpact
    {
        /// <summary>
        /// 飛ぶ弾が cause で終わったときに、その起爆を待つ爆発があれば point で爆発させる。normal は止まった面の向き (宙で終わったなら <see cref="Vector3.Zero"/>)。
        /// </summary>
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

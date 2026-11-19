using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerImpact
    {
        private readonly ServerCombat _combat;

        public ServerImpact(ServerCombat combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        public void Apply(in ServerShot shot, Vector3 direction, in ShotHit hit)
        {
            // 敵の置いた物は弾を受け止めて耐久で受け、着弾の効果 (爆発・状態異常・燃える範囲) を出さない (オフラインの Deployable.TryStopBullet と同じ)。
            // 飛ぶ弾の止まった所での起爆 (Detonate) は、受け止めた後にも起きる
            if (hit.HitDeployable)
            {
                _combat.DamageDeployable(shot, hit);
                return;
            }

            // 乗り物は Unity と同じく部位 (HitZone) を持つ的として距離減衰を掛け、そのあと当たった部位の倍率を掛ける
            var impactHit = hit.HitTarget ? ImpactHit.Character(hit.Zone, hit.Point, hit.Distance, _combat.ZoneDamage)
                : hit.HitVehicle ? ImpactHit.Character(HitZone.Body, hit.Point, hit.Distance, ZoneDamageMultipliers.Default)
                : ImpactHit.Structure(hit.Point, hit.Distance);
            var orders = ImpactResolver.Resolve(shot.Impact, impactHit, shot.Weapon.Trigger.Charge, shot.ChargeFraction);
            foreach (var order in orders)
            {
                switch (order.Kind)
                {
                    case ImpactOrderKind.Damage:
                        if (hit.HitVehicle)
                        {
                            _combat.DamageVehicle(shot, hit, order.Amount);
                        }
                        else
                        {
                            _combat.DamagePlayer(shot, direction, hit, order.Amount);
                        }

                        break;
                    case ImpactOrderKind.StructureDamage:
                        _combat.DamageStructure(hit.ColliderId, order.Point, order.Amount, order.Radius);
                        break;
                    case ImpactOrderKind.Explosion:
                        Explode(shot, hit, order);
                        break;
                    case ImpactOrderKind.StatusEffect:
                        Afflict(shot, hit, order);
                        break;
                    case ImpactOrderKind.AreaEffect:
                        Spread(shot, order);
                        break;
                    default:
                        throw new InvalidOperationException("unknown impact order " + order.Kind);
                }
            }
        }

        partial void Explode(in ServerShot shot, in ShotHit hit, ImpactOrder order);

        partial void Afflict(in ServerShot shot, in ShotHit hit, ImpactOrder order);

        partial void Spread(in ServerShot shot, ImpactOrder order);
    }
}

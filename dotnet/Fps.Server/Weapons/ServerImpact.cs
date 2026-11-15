using System;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// 弾が当たったときの効果。<see cref="ImpactResolver"/> が着弾の値から出した指示 (<see cref="ImpactOrder"/>) を、サーバーの上で順に行う。
    /// ダメージと建造物被害は <see cref="ServerCombat"/> に任せ、爆発・状態異常・範囲効果は機能ごとの partial が受け持つ。
    /// </summary>
    internal sealed partial class ServerImpact
    {
        private readonly ServerCombat _combat;

        public ServerImpact(ServerCombat combat)
        {
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        /// <summary>shot の弾が direction へ進んで hit に当たった。</summary>
        public void Apply(in ServerShot shot, Vector3 direction, in ShotHit hit)
        {
            Apply(shot, direction, hit, 1f);
        }

        /// <summary>shot の弾が direction へ進み、damageMultiplier だけ弱って hit に当たった。</summary>
        public void Apply(in ServerShot shot, Vector3 direction, in ShotHit hit, float damageMultiplier)
        {
            var impactHit = hit.HitTarget ? ImpactHit.Character(hit.Zone, hit.Point, hit.Distance) : ImpactHit.Structure(hit.Point, hit.Distance);
            var orders = ImpactResolver.Resolve(shot.Impact, impactHit, shot.Weapon.Trigger.Charge, shot.ChargeFraction, damageMultiplier);
            foreach (var order in orders)
            {
                switch (order.Kind)
                {
                    case ImpactOrderKind.Damage:
                        _combat.DamagePlayer(shot, direction, hit, order.Amount);
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

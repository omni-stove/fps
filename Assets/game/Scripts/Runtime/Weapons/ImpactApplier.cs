using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public static partial class ImpactApplier
    {
        public static ShotOutcome Apply(ShotRequest request, RaycastHit hit, Vector3 direction)
        {
            var point = hit.point.ToNumerics();
            var hitbox = Hitbox.FindFor(hit.collider);
            if (hitbox == null)
            {
                var structureOrders = ImpactResolver.Resolve(
                    request.Impact, ImpactHit.Structure(point, hit.distance), request.Charge, request.ChargeFraction);

                // 崩れた破片で倒したときに撃った者の名前でキルフィードに出せるよう、建物にも帰属を渡す。
                using (CombatEvents.Attribute(request.Shooter, request.WeaponId))
                {
                    Execute(structureOrders, request, hit, direction, null, null);
                }

                return new ShotOutcome(true, hit, null, null, 0, false, false);
            }

            var owner = hitbox.Owner;
            if (owner == null)
            {
                Debug.LogError($"{Damageable.LogTag} Hitbox '{hitbox.name}' has no Damageable owner; the hit is ignored", hitbox);
                return new ShotOutcome(true, hit, hitbox, null, 0, false, false);
            }

            var wasAlive = !owner.IsDead;
            var orders = ImpactResolver.Resolve(
                request.Impact,
                ImpactHit.Character(hitbox.Zone, point, hit.distance, owner.ZoneMultipliers),
                request.Charge,
                request.ChargeFraction);
            int damage;
            using (CombatEvents.Attribute(request.Shooter, request.WeaponId))
            {
                damage = Execute(orders, request, hit, direction, hitbox, owner);
            }

            return new ShotOutcome(true, hit, hitbox, owner, damage, wasAlive, wasAlive && owner.IsDead);
        }

        private static int Execute(
            IReadOnlyList<ImpactOrder> orders,
            ShotRequest request,
            RaycastHit hit,
            Vector3 direction,
            Hitbox hitbox,
            Damageable owner)
        {
            var damage = 0;
            foreach (var order in orders)
            {
                switch (order.Kind)
                {
                    case ImpactOrderKind.Damage:
                        damage += hitbox.ApplyResolvedHit(
                            DamageCalculator.Scale(order.Amount, hitbox.DamageMultiplier), request.SourcePosition, direction, DamageKind.Bullet);
                        break;
                    case ImpactOrderKind.StructureDamage:
                        StructureDamage.Apply(hit.collider, order.Point.ToUnity(), order.Amount, order.Radius);
                        break;
                    case ImpactOrderKind.Explosion:
                        ApplyExplosion(order, request, hit);
                        break;
                    case ImpactOrderKind.StatusEffect:
                        ApplyStatusEffect(order, request, owner);
                        break;
                    case ImpactOrderKind.AreaEffect:
                        ApplyAreaEffect(order, request);
                        break;
                }
            }

            return damage;
        }
    }
}

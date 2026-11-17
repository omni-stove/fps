using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class HitscanDelivery : IShotDelivery
    {
        public static readonly HitscanDelivery Instance = new HitscanDelivery();

        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            var origin = round.Origin.ToUnity();
            var direction = round.Direction.ToUnity();
            var range = request.Round.Range;
            var penetration = PenetrationRule.For(request.Round);
            if (penetration != null)
            {
                return DeliverPenetrating(request, penetration, round.Index, origin, direction, range);
            }

            var radius = request.Round.Radius;

            // ShotCast が向きと射程を検査するので、撃ったことはその後に知らせる (例外になった弾を撃ったことにしない)。
            var found = ShotCast.TryFirst(origin, direction, radius, range, request.LayerMask, request.ShooterRoot, out var hit);
            // 2 本目からのペレットは 1 回の射撃の続きなので、銃声・薬莢・反動・カメラの揺れを重ねず、トレーサーだけ出す。
            if (round.Index > 0)
            {
                CombatEvents.RaiseShotContinued(request.Shooter, origin, direction, request.WeaponId);
            }
            else
            {
                CombatEvents.RaiseShotFired(request.Shooter, origin, direction, request.WeaponId);
            }

            var travelled = 0f;
            var bounces = 0;
            var damageMultiplier = 1f;
            while (true)
            {
                Hitscan.ReportNearMisses(origin, direction, found ? hit.distance : range - travelled, request.ShooterRoot, found ? hit.collider : null);
                if (!found)
                {
                    return ShotOutcome.Miss;
                }

                travelled += hit.distance;
                if (!ShotCast.TryBounce(request.Round.Ricochet, bounces, direction, hit, range - travelled, out var bounced, out var multiplier))
                {
                    break;
                }

                CombatEvents.RaiseImpact(hit.point, hit.normal, hit.collider);
                bounces++;
                damageMultiplier *= multiplier;
                var surface = hit;
                origin = surface.point;
                direction = bounced;
                CombatEvents.RaiseShotContinued(request.Shooter, origin, direction, request.WeaponId);
                found = ShotCast.TryFirstAfterBounce(surface, direction, radius, range - travelled, request.LayerMask, request.ShooterRoot, out hit);
                if (found)
                {
                    hit.distance += ShotCast.RicochetSurfaceOffset;
                }
            }

            // 距離減衰は弾が跳ねながら進んだ道のりで決める。
            hit.distance = travelled;
            if (bounces > 0)
            {
                request = new ShotRequest(
                    request.Shooter,
                    request.ShooterRoot,
                    request.SourcePosition,
                    request.Round,
                    request.Impact.WithDamageMultiplier(damageMultiplier),
                    request.WeaponId,
                    request.LayerMask,
                    request.Charge,
                    request.ChargeFraction);
            }

            return Land(request, hit, direction);
        }

        // 飛ぶ弾も当たった所から先はヒットスキャンと同じ順 (盾 → 置いた物 → 着弾の効果) で受け止めるので、ここを共に使う。
        internal static ShotOutcome Land(ShotRequest request, RaycastHit hit, Vector3 direction)
        {
            CombatEvents.RaiseImpact(hit.point, hit.normal, hit.collider);
            var damage = request.Impact.Damage;
            if (BulletShield.TryBlock(hit.collider, damage, request.SourcePosition, request.Shooter))
            {
                return ShotOutcome.Stopped(hit);
            }

            if (Deployable.TryStopBullet(hit.collider, damage, hit.distance, request.Impact.Falloff, request.SourcePosition, direction, request.Shooter))
            {
                return ShotOutcome.Stopped(hit);
            }

            return ImpactApplier.Apply(request, hit, direction);
        }

        // 当てた相手が倒れた知らせから次の弾が撃たれても並びを壊さないよう、並びは弾ごとに持つ。止まった面で跳ねないのはサーバーと揃えるため。
        private static ShotOutcome DeliverPenetrating(
            ShotRequest request, PenetrationSpec penetration, int pelletIndex, Vector3 origin, Vector3 direction, float range)
        {
            var hits = new List<RaycastHit>();
            var struck = new HashSet<Damageable>();
            ShotCast.CastAll(origin, direction, range, request.LayerMask, request.ShooterRoot, hits);
            if (pelletIndex > 0)
            {
                CombatEvents.RaiseShotContinued(request.Shooter, origin, direction, request.WeaponId);
            }
            else
            {
                CombatEvents.RaiseShotFired(request.Shooter, origin, direction, request.WeaponId);
            }

            var outcome = ShotOutcome.Miss;
            var personOutcome = ShotOutcome.Miss;
            var multiplier = 1f;
            var passed = 0;
            var travelled = range;
            Collider stoppedBy = null;
            foreach (var hit in hits)
            {
                // 1 人の体は部位ごとのコライダーに分かれているので、同じ弾が同じ人に 2 度当たらないようにする。
                var hitbox = Hitbox.FindFor(hit.collider);
                var owner = hitbox != null ? hitbox.Owner : null;
                if (owner != null && struck.Contains(owner))
                {
                    continue;
                }

                if (!Strike(request, hit, direction, multiplier, out outcome))
                {
                    travelled = hit.distance;
                    stoppedBy = hit.collider;
                    break;
                }

                if (owner != null)
                {
                    struck.Add(owner);
                    if (personOutcome.Owner == null)
                    {
                        personOutcome = outcome;
                    }
                }

                // 人の厚さは部位のコライダーの重なりで測れないので 0 とし、抜けるかは素材と枚数だけで決める。
                var material = SurfaceMaterial.IdOf(hit.collider);
                var thickness = owner != null
                    ? 0f
                    : ShotCast.Thickness(hit.collider, hit.point, direction, PenetrationRule.MaxThicknessFor(penetration, material));
                if (!PenetrationRule.TryPassThrough(penetration, passed, material, thickness, out var passMultiplier))
                {
                    travelled = hit.distance;
                    stoppedBy = hit.collider;
                    break;
                }

                passed++;
                multiplier *= passMultiplier;
                Debug.Log($"{Damageable.LogTag} penetrate material={material} count={passed} multiplier={multiplier:F2}", hit.collider);
            }

            Hitscan.ReportNearMisses(origin, direction, travelled, request.ShooterRoot, stoppedBy);
            return personOutcome.Owner != null ? personOutcome : outcome;
        }

        private static bool Strike(ShotRequest request, RaycastHit hit, Vector3 direction, float multiplier, out ShotOutcome outcome)
        {
            var weakened = new ShotRequest(
                request.Shooter,
                request.ShooterRoot,
                request.SourcePosition,
                request.Round,
                request.Impact.WithDamageMultiplier(multiplier),
                request.WeaponId,
                request.LayerMask,
                request.Charge,
                request.ChargeFraction);
            CombatEvents.RaiseImpact(hit.point, hit.normal, hit.collider);
            var damage = weakened.Impact.Damage;
            if (BulletShield.TryBlock(hit.collider, damage, weakened.SourcePosition, weakened.Shooter)
                || Deployable.TryStopBullet(
                    hit.collider, damage, hit.distance, weakened.Impact.Falloff, weakened.SourcePosition, direction, weakened.Shooter))
            {
                outcome = ShotOutcome.Stopped(hit);
                return false;
            }

            outcome = ImpactApplier.Apply(weakened, hit, direction);
            return true;
        }
    }
}

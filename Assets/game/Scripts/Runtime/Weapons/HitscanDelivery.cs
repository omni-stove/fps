using System;
using System.Collections.Generic;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class HitscanDelivery : IShotDelivery
    {
        public static readonly HitscanDelivery Instance = new HitscanDelivery();

        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            var origin = round.Origin.ToUnity();
            var direction = round.Direction.ToUnity();
            var range = request.Round.Range;
            if (request.Round.Penetration != null)
            {
                return DeliverPenetrating(request, origin, direction, range);
            }

            // ShotCast が向きと射程を検査するので、撃ったことはその後に知らせる (例外になった弾を撃ったことにしない)。
            var found = ShotCast.TryFirst(origin, direction, request.Round.Radius, range, request.LayerMask, request.ShooterRoot, out var hit);
            CombatEvents.RaiseShotFired(request.Shooter, origin, direction, request.WeaponId);
            Hitscan.ReportNearMisses(origin, direction, found ? hit.distance : range, request.ShooterRoot, found ? hit.collider : null);
            if (!found)
            {
                return ShotOutcome.Miss;
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

        // 当たった物を近い順に抜けられるだけ抜ける。撃った本人に返すのは最初に当てた人の結果で、人に当たらなければ止まった所の結果。
        // 当てた相手が倒れた知らせから次の弾が撃たれても並びを壊さないよう、並びは弾ごとに持つ。
        private static ShotOutcome DeliverPenetrating(ShotRequest request, Vector3 origin, Vector3 direction, float range)
        {
            var hits = new List<RaycastHit>();
            var struck = new HashSet<Damageable>();
            ShotCast.CastAll(origin, direction, range, request.LayerMask, request.ShooterRoot, hits);
            CombatEvents.RaiseShotFired(request.Shooter, origin, direction, request.WeaponId);

            var penetration = request.Round.Penetration;
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

        // 弾を受け止める物 (盾・置いた物) に止められたら false。
        private static bool Strike(ShotRequest request, RaycastHit hit, Vector3 direction, float multiplier, out ShotOutcome outcome)
        {
            CombatEvents.RaiseImpact(hit.point, hit.normal, hit.collider);
            var damage = Scale(request.Impact.Damage, multiplier);
            if (BulletShield.TryBlock(hit.collider, damage, request.SourcePosition, request.Shooter)
                || Deployable.TryStopBullet(hit.collider, damage, hit.distance, request.Impact.Falloff, request.SourcePosition, direction, request.Shooter))
            {
                outcome = ShotOutcome.Stopped(hit);
                return false;
            }

            outcome = ImpactApplier.Apply(request, hit, direction, multiplier);
            return true;
        }

        private static int Scale(int damage, float multiplier)
        {
            var scaled = Math.Round((double)damage * multiplier, MidpointRounding.AwayFromZero);
            return scaled >= int.MaxValue ? int.MaxValue : (int)scaled;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerShotTrace
    {
        // 当たった点から撃ち直すと、浮動小数の誤差で跳ねた面そのものにもう一度当たることがあるので、面から少し離して撃つ (Unity の ShotCast と同じ値)。
        public const float RicochetSurfaceOffset = 0.01f;

        partial void Ricochet(RoundSpec round, float tick, int shooterId, Func<int, bool> hittable, List<ShotTraceSegment> segments, int first)
        {
            // 貫通する弾は止まった面で跳ねない (オフラインの HitscanDelivery と同じ)。
            var ricochet = round.Ricochet;
            if (ricochet == null || segments.Count <= first || PenetrationRule.For(round) != null)
            {
                return;
            }

            var bounces = 0;
            var damageMultiplier = 1f;
            while (true)
            {
                var last = segments[segments.Count - 1];

                // 人・乗り物・置いた物に当たった弾はそこで止まり、ダメージを与える。跳ねるのは世界の面だけ。
                if (!last.HasHit || last.Hit.HitTarget || last.Hit.HitVehicle || last.Hit.HitDeployable)
                {
                    return;
                }

                var travelled = last.Hit.Distance;
                var remaining = round.Range - travelled;
                if (!(remaining > RicochetSurfaceOffset)
                    || !RicochetRule.TryBounce(ricochet, bounces, last.Direction, last.Hit.Normal, out var bounced, out var multiplier))
                {
                    return;
                }

                bounces++;
                damageMultiplier *= multiplier;
                segments[segments.Count - 1] = last.AsBounced();

                var surface = last.Hit.Point;
                var origin = surface + (Vector3.Normalize(last.Hit.Normal) * RicochetSurfaceOffset);
                _hits.Clear();
                if (!_hitboxes.Cast(origin, bounced, round.Radius, remaining - RicochetSurfaceOffset, 1, tick, shooterId, hittable, _hits))
                {
                    segments.Add(ShotTraceSegment.Miss(surface, bounced, damageMultiplier));
                    return;
                }

                // 距離減衰は弾が跳ねながら進んだ道のりで決める。
                var hit = WithDistance(_hits[0], travelled + RicochetSurfaceOffset + _hits[0].Distance);
                segments.Add(ShotTraceSegment.WithHit(surface, bounced, hit, damageMultiplier));
            }
        }

        private static ShotHit WithDistance(in ShotHit hit, float distance)
        {
            var moved = hit.HitTarget
                ? ShotHit.Target(hit.Point, hit.Normal, distance, hit.TargetId, hit.TargetPosition, hit.TargetCrouched, hit.Zone)
                : hit.HitVehicle
                    ? ShotHit.Vehicle(hit.Point, hit.Normal, distance, hit.VehicleIndex, hit.DamageMultiplier, hit.VehiclePartId)
                    : hit.HitDeployable
                        ? ShotHit.Deployable(hit.Point, hit.Normal, distance, hit.DeployableId)
                        : ShotHit.World(new WorldHit(hit.Point, hit.Normal, distance, hit.ColliderId, hit.Layer));
            return moved.WithRewind(hit.RewindTicks);
        }
    }
}

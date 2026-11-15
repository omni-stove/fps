using Fps.Core;

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

            // ShotCast が向きと射程を検査するので、撃ったことはその後に知らせる (例外になった弾を撃ったことにしない)。
            var found = ShotCast.TryFirst(origin, direction, range, request.LayerMask, request.ShooterRoot, out var hit);
            CombatEvents.RaiseShotFired(request.Shooter, origin, direction, request.WeaponId);

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

                CombatEvents.RaiseImpact(hit.point, hit.normal, hit.collider);
                travelled += hit.distance;
                if (!ShotCast.TryBounce(request.Round.Ricochet, bounces, direction, hit, range - travelled, out var bounced, out var multiplier))
                {
                    break;
                }

                bounces++;
                damageMultiplier *= multiplier;
                var surface = hit;
                origin = surface.point;
                direction = bounced;
                CombatEvents.RaiseShotContinued(request.Shooter, origin, direction, request.WeaponId);
                found = ShotCast.TryFirstAfterBounce(surface, direction, range - travelled, request.LayerMask, request.ShooterRoot, out hit);
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
    }
}

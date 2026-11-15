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
            var found = ShotCast.TryFirst(origin, direction, request.Round.Radius, range, request.LayerMask, request.ShooterRoot, out var hit);
            CombatEvents.RaiseShotFired(request.Shooter, origin, direction, request.WeaponId);
            Hitscan.ReportNearMisses(origin, direction, found ? hit.distance : range, request.ShooterRoot, found ? hit.collider : null);
            if (!found)
            {
                return ShotOutcome.Miss;
            }

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
    }
}

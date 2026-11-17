using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class BeamDelivery : IShotDelivery
    {
        public static readonly BeamDelivery Instance = new BeamDelivery();

        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            var origin = round.Origin.ToUnity();
            var direction = round.Direction.ToUnity();
            var range = request.Round.Range;

            // 照射のたびに撃った時点の視線で当たり直すので、照準を振れば当たる相手も線の先も追って変わる。
            var found = ShotCast.TryFirst(origin, direction, request.Round.Radius, range, request.LayerMask, request.ShooterRoot, out var hit);
            var end = found ? hit.point : origin + (direction.normalized * range);
            ShowLine(request, origin, direction, end);

            // 撃った・当たったの知らせ (CombatEvents.ShotFired / Impact) を出さないのは、受け手が 1 発の弾として銃声・曳光弾・薬莢・火花を
            // 照射の回数だけ出してしまうため。ビームの見た目と音は線 (BeamLine) が持つ。
            if (!found)
            {
                return ShotOutcome.Miss;
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

        private static void ShowLine(ShotRequest request, Vector3 origin, Vector3 direction, Vector3 end)
        {
            // 届け方が Beam でない弾をこのフックに直接渡されたときは照射の間隔が無いので、線は 1 フレームぶんだけ残す。
            var beam = request.Round.Beam;
            var tickSeconds = beam != null ? beam.TickSeconds : Mathf.Max(Time.deltaTime, Time.fixedDeltaTime);

            // 目の位置から線を出すと一人称では線が画面の中心に潰れて見えないので、銃口から出す。
            var presenter = WeaponPresentation.FindFor(request.Shooter);
            var sockets = presenter != null ? presenter.Sockets : WeaponSockets.None;
            var from = sockets.MuzzlePose(origin, direction).position;
            BeamLine.Show(request.Shooter, request.WeaponId, tickSeconds, from, end, Time.time);
        }
    }
}

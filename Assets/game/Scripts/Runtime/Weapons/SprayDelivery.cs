using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class SprayDelivery : IShotDelivery
    {
        // これを超える数のコライダーが射程の球に入ると遠いものを取りこぼす。毎回の確保を避けるため上限を決めて使い回す。
        public const int MaxColliders = 64;

        // 体の表面へ線を引くと丸めで手前に止まり表面に届かないことがあるので、少し奥まで引く。
        private const float _surfaceReach = 0.05f;

        public static readonly SprayDelivery Instance = new SprayDelivery();

        private static readonly Collider[] _overlap = new Collider[MaxColliders];
        private static readonly RaycastHit[] _rays = new RaycastHit[ShotCast.MaxHits];
        private static readonly List<Target> _targets = new List<Target>();

        public ShotOutcome Deliver(ShotRequest request, PlannedRound round)
        {
            // 届け方が Spray でない弾をこのフックに直接渡されたときは円錐が無いので、1 本の線で届ける。
            var spray = request.Round.Spray;
            if (spray == null)
            {
                return HitscanDelivery.Instance.Deliver(request, round);
            }

            var origin = round.Origin.ToUnity();
            var direction = round.Direction.ToUnity().normalized;
            var range = request.Round.Range;
            ShowFlame(request, spray, origin, direction, WallDistance(request, origin, direction, range));

            // 撃った・当たったの知らせ (CombatEvents.ShotFired / Impact) を出さないのは、受け手が 1 発の弾として銃声・曳光弾・薬莢・火花を
            // 噴射の回数だけ出してしまうため。火炎の見た目と音は SprayFlame が持つ。
            FindTargets(request, spray, origin, direction, range);
            var first = ShotOutcome.Miss;
            for (var i = 0; i < _targets.Count; i++)
            {
                var target = _targets[i];
                var outcome = ImpactApplier.Apply(request, target.Hit, target.Direction);
                if (i == 0)
                {
                    first = outcome;
                }
                else
                {
                    // 1 回の噴射で何人も焼けるが戻り値は 1 つなので、残りは飛ぶ弾の着弾と同じ口で撃った側へ知らせる。
                    request.Landed?.Invoke(outcome);
                }
            }

            _targets.Clear();
            return first;
        }

        private static void FindTargets(ShotRequest request, SpraySpec spray, Vector3 origin, Vector3 direction, float range)
        {
            _targets.Clear();
            var count = Physics.OverlapSphereNonAlloc(origin, range, _overlap, request.LayerMask, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < count; i++)
            {
                var collider = _overlap[i];
                if (request.ShooterRoot != null && collider.transform.IsChildOf(request.ShooterRoot))
                {
                    continue;
                }

                var hitbox = Hitbox.FindFor(collider);
                var owner = hitbox != null ? hitbox.Owner : null;
                if (owner == null || Contains(owner))
                {
                    continue;
                }

                var point = NearestPoint(collider, origin, direction, range);
                if (!SprayCone.Contains(spray, range, origin.ToNumerics(), direction.ToNumerics(), point.ToNumerics()))
                {
                    continue;
                }

                // 体のこちら側までの線が先に別の物 (壁・盾・別の相手) に当たれば、陰にいるので焼かない。
                var toPoint = point - origin;
                var distance = toPoint.magnitude;
                var toward = distance > 1e-4f ? toPoint / distance : direction;
                if (!ShotCast.TryFirst(origin, toward, distance + _surfaceReach, request.LayerMask, request.ShooterRoot, out var hit))
                {
                    continue;
                }

                var reached = Hitbox.FindFor(hit.collider);
                if (reached == null || reached.Owner != owner)
                {
                    continue;
                }

                _targets.Add(new Target(owner, hit, toward));
            }

            Array.Clear(_overlap, 0, count);
            _targets.Sort((a, b) => a.Hit.distance.CompareTo(b.Hit.distance));
        }

        private static bool Contains(Damageable owner)
        {
            foreach (var target in _targets)
            {
                if (target.Owner == owner)
                {
                    return true;
                }
            }

            return false;
        }

        // 体の中心でなく軸にいちばん近い表面で確かめるのは、体の端が円錐にかかっていれば焼くため。
        private static Vector3 NearestPoint(Collider collider, Vector3 origin, Vector3 direction, float range)
        {
            var along = Mathf.Clamp(Vector3.Dot(collider.bounds.center - origin, direction), 0f, range);
            var onAxis = origin + (direction * along);

            // 凸でないメッシュは ClosestPoint が使えないので、囲む箱で近い点を取る。
            return collider is MeshCollider mesh && !mesh.convex ? collider.bounds.ClosestPoint(onAxis) : collider.ClosestPoint(onAxis);
        }

        // 人の体は火炎を止めないので、撃った者と当たり判定 (Hitbox) を素通りして、最初に当たる物までの距離を火炎の長さにする。
        private static float WallDistance(ShotRequest request, Vector3 origin, Vector3 direction, float range)
        {
            var count = Physics.RaycastNonAlloc(origin, direction, _rays, range, request.LayerMask, QueryTriggerInteraction.Ignore);
            var nearest = range;
            for (var i = 0; i < count; i++)
            {
                var hit = _rays[i];
                if (hit.distance >= nearest
                    || (request.ShooterRoot != null && hit.collider.transform.IsChildOf(request.ShooterRoot))
                    || Hitbox.FindFor(hit.collider) != null)
                {
                    continue;
                }

                nearest = hit.distance;
            }

            Array.Clear(_rays, 0, count);
            return nearest;
        }

        private static void ShowFlame(ShotRequest request, SpraySpec spray, Vector3 origin, Vector3 direction, float length)
        {
            // 目の位置から出すと一人称では火炎が画面の中心に潰れて見えないので、銃口から出す。
            var presenter = WeaponPresentation.FindFor(request.Shooter);
            var sockets = presenter != null ? presenter.Sockets : WeaponSockets.None;
            var from = sockets.MuzzlePose(origin, direction).position;
            SprayFlame.Show(request.Shooter, request.WeaponId, spray.TickSeconds, from, direction, length, spray.ConeDegrees, Time.time);
        }

        private readonly struct Target
        {
            public Target(Damageable owner, RaycastHit hit, Vector3 direction)
            {
                Owner = owner;
                Hit = hit;
                Direction = direction;
            }

            public Damageable Owner { get; }

            public RaycastHit Hit { get; }

            public Vector3 Direction { get; }
        }
    }
}

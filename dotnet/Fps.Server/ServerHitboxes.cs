using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
{
    /// <summary>
    /// ラグ補償の当たり判定 (Unity の <c>HitboxRewinder</c> と NetTeamDeathmatch.Fire の記録)。各プレイヤーの体の位置としゃがみを tick ごとに
    /// <see cref="LagCompensator"/> に残し、撃った人が見ていた tick まで巻き戻したカプセルと、<c>.fpsmap</c> の世界への Raycast の近い方を当たりにする。
    /// </summary>
    /// <remarks>
    /// 巻き戻した位置が無い対象 (その時点にまだいなかった・置き直した直後) と当てられない対象は素通りする。撃った本人にも当たらない。
    /// 置き直すと前の位置との間を補間しないよう、その人の記録を捨てる。
    /// </remarks>
    internal sealed class ServerHitboxes
    {
        /// <summary>Unity の Physics.DefaultRaycastLayers と同じく Ignore Raycast (2) だけを外す。</summary>
        public const int RaycastLayers = ~(1 << 2);

        private const int _noTick = int.MinValue;

        private readonly ServerBodies _bodies;
        private readonly IWorldCollision _world;
        private readonly LagCompensator _lag;
        private readonly List<int> _ids = new List<int>();

        public ServerHitboxes(ServerBodies bodies, IWorldCollision world, LagCompensator lag = null)
        {
            _bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lag = lag ?? new LagCompensator();
        }

        /// <summary>巻き戻せる最大の tick 数。Unity のサーバーと同じ <see cref="LagCompensator.DefaultMaxRewindTicks"/>。</summary>
        public int MaxRewindTicks => _lag.MaxRewindTicks;

        public bool HasHistory => LastRecordedTick != _noTick;

        public int LastRecordedTick { get; private set; } = _noTick;

        /// <summary>出現した全員の今の位置と姿勢を tick として残す。tick は前回以上。</summary>
        public void Record(int tick)
        {
            if (HasHistory && tick < LastRecordedTick)
            {
                throw new ArgumentOutOfRangeException(nameof(tick), tick, "tick must not be older than the last recorded tick");
            }

            foreach (var body in _bodies.All)
            {
                if (body.HasSpawned)
                {
                    _lag.Record(tick, body.PlayerId, body.Position, body.Stance != PlayerStance.Standing);
                }
            }

            LastRecordedTick = tick;
        }

        public void Forget(int playerId)
        {
            _lag.Remove(playerId);
        }

        /// <summary>
        /// 撃った人が見ていた tick (補間の割合を含む。<see cref="MaxRewindTicks"/> より古ければそこまでに縮める) まで巻き戻した対象と世界に、
        /// origin から direction へ range まで線を引き、いちばん近い当たりを返す。対象は hittable が true を返す人だけで、撃った本人は除く。
        /// </summary>
        public bool Raycast(
            Vector3 origin, Vector3 direction, float range, float tick, int shooterId, Func<int, bool> hittable, out ShotHit hit)
        {
            ArgumentNullException.ThrowIfNull(hittable);
            if (!IsFinite(origin))
            {
                throw new ArgumentException("origin must be finite", nameof(origin));
            }

            if (!IsFinite(direction) || direction.LengthSquared() < 1e-12f)
            {
                throw new ArgumentException("direction must be finite and not zero", nameof(direction));
            }

            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            var unit = Vector3.Normalize(direction);
            hit = default;
            var found = false;
            if (_world.Raycast(origin, unit, range, RaycastLayers, out var world))
            {
                hit = ShotHit.World(world);
                found = true;
            }

            if (!HasHistory)
            {
                return found;
            }

            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            var rewound = _lag.Rewind(clamped);
            var crouched = _lag.RewindCrouched(clamped);
            _ids.Clear();
            foreach (var body in _bodies.All)
            {
                _ids.Add(body.PlayerId);
            }

            var geometry = _bodies.Geometry;
            foreach (var id in _ids)
            {
                if (id == shooterId || !rewound.TryGetValue(id, out var position) || !hittable(id))
                {
                    continue;
                }

                var isCrouched = crouched.TryGetValue(id, out var c) && c;
                var shape = geometry.Shape(isCrouched);
                geometry.SphereCenters(shape, out var bottomY, out var topY);
                var p = position + new Vector3(0f, bottomY, 0f);
                var q = position + new Vector3(0f, topY, 0f);
                var limit = found ? hit.Distance : range;
                if (!RayCapsule(origin, unit, p, q, geometry.Radius, limit, out var distance, out var normal))
                {
                    continue;
                }

                var point = origin + (unit * distance);
                var bottom = position.Y + shape.CenterY - (shape.Height / 2f);
                hit = ShotHit.Target(point, normal, distance, id, position, isCrouched, Zone(point.Y, bottom, shape.Height));
                found = true;
            }

            hit = hit.WithRewind(LastRecordedTick - clamped);
            return found;
        }

        private static HitZone Zone(float hitY, float bottom, float height)
        {
            return height > 0f && float.IsFinite(height) ? CapsuleHitZone.Classify(hitY, bottom, height) : HitZone.Body;
        }

        /// <summary>
        /// 単位ベクトル direction の線と、p・q を両端の球の中心とする半径 radius のカプセルが maxDistance までに交わるか。
        /// 始点がカプセルの中なら距離 0 で当たる。
        /// </summary>
        internal static bool RayCapsule(
            Vector3 origin, Vector3 direction, Vector3 p, Vector3 q, float radius, float maxDistance, out float distance, out Vector3 normal)
        {
            distance = 0f;
            normal = -direction;
            var axis = q - p;
            var axisLengthSq = axis.LengthSquared();
            var inside = ClosestOnSegment(origin, p, q, axis, axisLengthSq);
            if (Vector3.DistanceSquared(origin, inside) <= radius * radius)
            {
                return true;
            }

            var best = float.PositiveInfinity;
            if (axisLengthSq > 1e-12f)
            {
                // 端を切った円柱の側面
                var m = origin - p;
                var md = Vector3.Dot(m, axis);
                var nd = Vector3.Dot(direction, axis);
                var a = axisLengthSq - (nd * nd);
                var k = Vector3.Dot(m, m) - (radius * radius);
                var b = (axisLengthSq * Vector3.Dot(m, direction)) - (nd * md);
                var c = (axisLengthSq * k) - (md * md);
                if (Math.Abs(a) > 1e-12f)
                {
                    var discriminant = (b * b) - (a * c);
                    if (discriminant >= 0f)
                    {
                        var t = (-b - MathF.Sqrt(discriminant)) / a;
                        var along = md + (t * nd);
                        if (t >= 0f && along >= 0f && along <= axisLengthSq)
                        {
                            best = t;
                        }
                    }
                }
            }

            best = Math.Min(best, RaySphere(origin, direction, p, radius));
            best = Math.Min(best, RaySphere(origin, direction, q, radius));
            if (!(best <= maxDistance))
            {
                return false;
            }

            distance = best;
            var point = origin + (direction * best);
            var center = ClosestOnSegment(point, p, q, axis, axisLengthSq);
            var outward = point - center;
            normal = outward.LengthSquared() > 1e-12f ? Vector3.Normalize(outward) : -direction;
            return true;
        }

        private static float RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
        {
            var m = origin - center;
            var b = Vector3.Dot(m, direction);
            var c = Vector3.Dot(m, m) - (radius * radius);
            if (c > 0f && b > 0f)
            {
                return float.PositiveInfinity;
            }

            var discriminant = (b * b) - c;
            if (discriminant < 0f)
            {
                return float.PositiveInfinity;
            }

            return Math.Max(0f, -b - MathF.Sqrt(discriminant));
        }

        private static Vector3 ClosestOnSegment(Vector3 point, Vector3 p, Vector3 q, Vector3 axis, float axisLengthSq)
        {
            if (axisLengthSq <= 1e-12f)
            {
                return p;
            }

            var t = Math.Clamp(Vector3.Dot(point - p, axis) / axisLengthSq, 0f, 1f);
            return p + (axis * t);
        }

        private static bool IsFinite(Vector3 v)
        {
            return float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        }
    }

    /// <summary><see cref="ServerHitboxes.Raycast"/> が当たった 1 か所。</summary>
    internal readonly struct ShotHit
    {
        private ShotHit(
            Vector3 point, Vector3 normal, float distance, bool hitTarget, int targetId, Vector3 targetPosition, bool targetCrouched,
            HitZone zone, int colliderId, int layer, float rewindTicks)
        {
            Point = point;
            Normal = normal;
            Distance = distance;
            HitTarget = hitTarget;
            TargetId = targetId;
            TargetPosition = targetPosition;
            TargetCrouched = targetCrouched;
            Zone = zone;
            ColliderId = colliderId;
            Layer = layer;
            RewindTicks = rewindTicks;
        }

        public Vector3 Point { get; }

        public Vector3 Normal { get; }

        public float Distance { get; }

        /// <summary>プレイヤーに当たったか。false なら世界 (<see cref="ColliderId"/>) に当たった。</summary>
        public bool HitTarget { get; }

        public int TargetId { get; }

        /// <summary>判定に使った (巻き戻した) 当たった人の位置。</summary>
        public Vector3 TargetPosition { get; }

        public bool TargetCrouched { get; }

        public HitZone Zone { get; }

        public int ColliderId { get; }

        public int Layer { get; }

        /// <summary>巻き戻した tick 数 (記録した最新の tick − 判定に使った tick)。</summary>
        public float RewindTicks { get; }

        public static ShotHit World(WorldHit hit)
        {
            return new ShotHit(hit.Point, hit.Normal, hit.Distance, false, 0, default, false, HitZone.Body, hit.ColliderId, hit.Layer, 0f);
        }

        public static ShotHit Target(
            Vector3 point, Vector3 normal, float distance, int targetId, Vector3 targetPosition, bool crouched, HitZone zone)
        {
            return new ShotHit(point, normal, distance, true, targetId, targetPosition, crouched, zone, -1, 0, 0f);
        }

        public ShotHit WithRewind(float rewindTicks)
        {
            return new ShotHit(
                Point, Normal, Distance, HitTarget, TargetId, TargetPosition, TargetCrouched, Zone, ColliderId, Layer, rewindTicks);
        }
    }
}

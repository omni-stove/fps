using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 置き直すと前の位置との間を補間しないよう、その人の記録を捨てる。
    internal sealed partial class ServerHitboxes
    {
        // Unity の Physics.DefaultRaycastLayers と同じく Ignore Raycast (2) だけを外す。
        public const int RaycastLayers = ~(1 << 2);

        private const int _noTick = int.MinValue;

        private readonly ServerBodies _bodies;
        private readonly IWorldCollision _world;
        private readonly LagCompensator _lag;
        private readonly List<int> _ids = new List<int>();
        private readonly ServerVehicles _vehicles;

        public ServerHitboxes(ServerBodies bodies, IWorldCollision world, LagCompensator lag = null, ServerVehicles vehicles = null)
        {
            _vehicles = vehicles;
            _bodies = bodies ?? throw new ArgumentNullException(nameof(bodies));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _lag = lag ?? new LagCompensator();
        }

        public int MaxRewindTicks => _lag.MaxRewindTicks;

        public bool HasHistory => LastRecordedTick != _noTick;

        public int LastRecordedTick { get; private set; } = _noTick;

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

        public bool TryRewindCenter(int playerId, float tick, out Vector3 center)
        {
            center = default;
            if (!HasHistory)
            {
                return false;
            }

            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            if (!_lag.Rewind(clamped).TryGetValue(playerId, out var position))
            {
                return false;
            }

            var crouched = _lag.RewindCrouched(clamped).TryGetValue(playerId, out var c) && c;
            center = position + new Vector3(0f, _bodies.Geometry.Shape(crouched).CenterY, 0f);
            return true;
        }

        public void Forget(int playerId)
        {
            _lag.Remove(playerId);
        }

        public bool Cast(
            Vector3 origin, Vector3 direction, float radius, float range, int maxHits, float tick, int shooterId, Func<int, bool> hittable,
            List<ShotHit> hits)
        {
            ArgumentNullException.ThrowIfNull(hits);
            if (!(radius >= 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a non-negative finite number");
            }

            if (maxHits < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHits), maxHits, "maxHits must be at least 1");
            }

            var count = hits.Count;
            var handled = false;
            if (radius > 0f)
            {
                CastSphere(origin, direction, radius, range, maxHits, tick, shooterId, hittable, hits, ref handled);
            }
            else if (maxHits > 1)
            {
                CastMany(origin, direction, range, maxHits, tick, shooterId, hittable, hits, ref handled);
            }

            if (!handled && Raycast(origin, direction, range, tick, shooterId, hittable, out var hit))
            {
                hits.Add(hit);
            }

            return hits.Count > count;
        }

        // 太い弾の判定。埋まるまでは handled を立てず、細い線の判定で代える
        partial void CastSphere(
            Vector3 origin, Vector3 direction, float radius, float range, int maxHits, float tick, int shooterId, Func<int, bool> hittable,
            List<ShotHit> hits, ref bool handled);

        // 1 本の線で複数の当たりを拾う判定。埋まるまでは handled を立てず、最初の当たりだけを拾う
        partial void CastMany(
            Vector3 origin, Vector3 direction, float range, int maxHits, float tick, int shooterId, Func<int, bool> hittable,
            List<ShotHit> hits, ref bool handled);

        public bool Raycast(
            Vector3 origin, Vector3 direction, float range, float tick, int shooterId, Func<int, bool> hittable, out ShotHit hit)
        {
            Validate(origin, direction, range, hittable);
            return Nearest(origin, Vector3.Normalize(direction), 0f, range, tick, shooterId, hittable, out hit);
        }

        private static void Validate(Vector3 origin, Vector3 direction, float range, Func<int, bool> hittable)
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
        }

        private bool Nearest(
            Vector3 origin, Vector3 unit, float radius, float range, float tick, int shooterId, Func<int, bool> hittable, out ShotHit hit)
        {
            hit = default;
            var found = false;
            if (_world.Raycast(origin, unit, range, RaycastLayers, out var world))
            {
                hit = ShotHit.World(world);
                found = true;
            }

            if (radius > 0f)
            {
                found |= NearerWorldSphere(origin, unit, radius, found ? hit.Distance : range, ref hit);
            }

            if (!HasHistory)
            {
                return found;
            }

            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            found |= NearerVehicle(origin, unit, found ? hit.Distance : range, clamped, shooterId, ref hit);
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
                var thickness = radius > 0f && StartsGrazing(origin, p, q, geometry.Radius, radius) ? 0f : radius;

                // 球と体のカプセルが触れるのは、球の中心の線が体を弾の半径だけ太らせたカプセルに触れるとき
                if (!RayCapsule(origin, unit, p, q, geometry.Radius + thickness, limit, out var distance, out var normal))
                {
                    continue;
                }

                var point = origin + (unit * distance) - (normal * thickness);
                var bottom = position.Y + shape.CenterY - (shape.Height / 2f);
                hit = ShotHit.Target(point, normal, distance, id, position, isCrouched, Zone(point.Y, bottom, shape.Height));
                found = true;
            }

            hit = hit.WithRewind(LastRecordedTick - clamped);
            return found;
        }

        // 車体は人より手前で弾を止めるので、人の当たりより先に、世界の当たりと比べて近い方を残す。
        private bool NearerVehicle(Vector3 origin, Vector3 unit, float limit, float tick, int shooterId, ref ShotHit hit)
        {
            if (_vehicles == null
                || !_vehicles.Raycast(origin, unit, limit, tick, shooterId, out var vehicle, out var distance, out var normal, out var multiplier))
            {
                return false;
            }

            hit = ShotHit.Vehicle(origin + (unit * distance), normal, distance, vehicle, multiplier);
            return true;
        }

        private static HitZone Zone(float hitY, float bottom, float height)
        {
            return height > 0f && float.IsFinite(height) ? CapsuleHitZone.Classify(hitY, bottom, height) : HitZone.Body;
        }

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

        // Unity の ShotCast と同じく、始めから弾の太さの分だけ重なっている体は球では拾わず、細い線でだけ拾う。
        private static bool StartsGrazing(Vector3 origin, Vector3 p, Vector3 q, float bodyRadius, float roundRadius)
        {
            var axis = q - p;
            var distanceSq = Vector3.DistanceSquared(origin, ClosestOnSegment(origin, p, q, axis, axis.LengthSquared()));
            var outer = bodyRadius + roundRadius;
            return distanceSq > bodyRadius * bodyRadius && distanceSq <= outer * outer;
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

    internal readonly struct ShotHit
    {
        private ShotHit(
            Vector3 point, Vector3 normal, float distance, bool hitTarget, int targetId, Vector3 targetPosition, bool targetCrouched,
            HitZone zone, int colliderId, int layer, float rewindTicks, int vehicle = -1, float damageMultiplier = 1f)
        {
            VehicleIndex = vehicle;
            DamageMultiplier = damageMultiplier;
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

        public bool HitTarget { get; }

        public int TargetId { get; }

        public Vector3 TargetPosition { get; }

        public bool TargetCrouched { get; }

        public HitZone Zone { get; }

        public int ColliderId { get; }

        public int Layer { get; }

        public float RewindTicks { get; }

        public int VehicleIndex { get; }

        public bool HitVehicle => VehicleIndex >= 0;

        public float DamageMultiplier { get; }

        public static ShotHit World(WorldHit hit)
        {
            return new ShotHit(hit.Point, hit.Normal, hit.Distance, false, 0, default, false, HitZone.Body, hit.ColliderId, hit.Layer, 0f);
        }

        public static ShotHit Target(
            Vector3 point, Vector3 normal, float distance, int targetId, Vector3 targetPosition, bool crouched, HitZone zone)
        {
            return new ShotHit(point, normal, distance, true, targetId, targetPosition, crouched, zone, -1, 0, 0f);
        }

        public static ShotHit Vehicle(Vector3 point, Vector3 normal, float distance, int vehicle, float damageMultiplier)
        {
            return new ShotHit(point, normal, distance, false, 0, default, false, HitZone.Body, -1, 0, 0f, vehicle, damageMultiplier);
        }

        public ShotHit WithRewind(float rewindTicks)
        {
            return new ShotHit(
                Point, Normal, Distance, HitTarget, TargetId, TargetPosition, TargetCrouched, Zone, ColliderId, Layer, rewindTicks, VehicleIndex,
                DamageMultiplier);
        }
    }
}

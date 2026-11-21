using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

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
        private readonly List<(int Id, RewoundBody Body)> _rewound = new List<(int Id, RewoundBody Body)>();
        private readonly ServerVehicles _vehicles;
        private readonly ServerDeployables _deployables;
        private readonly MotorSpec _motor;

        public ServerHitboxes(
            ServerBodies bodies,
            IWorldCollision world,
            LagCompensator lag = null,
            ServerVehicles vehicles = null,
            MotorSpec motor = null,
            ServerDeployables deployables = null)
        {
            _vehicles = vehicles;
            _deployables = deployables;
            _motor = motor ?? MotorSpec.Default;
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
                    _lag.Record(
                        tick, body.PlayerId, body.Position, body.Stance != PlayerStance.Standing, LeanStep.EyeOffset(body.Lean, body.Yaw, _motor));
                    RecordShield(tick, body);
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
            var lean = _lag.RewindLean(clamped).TryGetValue(playerId, out var l) ? l : Vector3.Zero;
            center = Rewound(position, crouched, lean).Center;
            return true;
        }

        public void Forget(int playerId)
        {
            _lag.Remove(playerId);
            _shields.Remove(playerId);
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
            return Nearest(origin, Vector3.Normalize(direction), 0f, range, tick, shooterId, hittable, true, out hit);
        }

        // 置いた物の撃つ弾は、撃った物自身を飛ばして持ち主の陣営で通すかを決めるので、置いた物の当たりは撃つ側 (ServerDeployables) が見る
        public bool RaycastIgnoringDeployables(
            Vector3 origin, Vector3 direction, float range, float tick, int shooterId, Func<int, bool> hittable, out ShotHit hit)
        {
            Validate(origin, direction, range, hittable);
            return Nearest(origin, Vector3.Normalize(direction), 0f, range, tick, shooterId, hittable, false, out hit);
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
            Vector3 origin, Vector3 unit, float radius, float range, float tick, int shooterId, Func<int, bool> hittable, bool deployables,
            out ShotHit hit)
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

            // 置いた物は動かず巻き戻さないので、記録がまだ無くても当てる
            if (deployables)
            {
                found |= NearerDeployable(origin, unit, found ? hit.Distance : range, shooterId, ref hit);
            }

            if (!HasHistory)
            {
                return found;
            }

            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            found |= NearerVehicle(origin, unit, found ? hit.Distance : range, clamped, shooterId, ref hit);
            foreach (var (id, body) in RewindBodies(clamped, shooterId, hittable))
            {
                var limit = found ? hit.Distance : range;
                var thickness = radius > 0f && StartsGrazing(origin, body.Bottom, body.Top, body.Radius, radius) ? 0f : radius;

                // 球と体のカプセルが触れるのは、球の中心の線が体を弾の半径だけ太らせたカプセルに触れるとき
                if (!RayCapsule(origin, unit, body.Bottom, body.Top, body.Radius + thickness, limit, out var distance, out var normal))
                {
                    continue;
                }

                var point = origin + (unit * distance) - (normal * thickness);
                hit = ShotHit.Target(point, normal, distance, id, body.Position, body.Crouched, body.Zone(point));
                found = true;
            }

            // 盾の板は持ち主の体より前にあるので、体の当たりを決めた後に、それより手前で板に当たるかを見る
            found |= NearerHeldShield(origin, unit, radius, found ? hit.Distance : range, clamped, _rewound, ref hit);
            hit = hit.WithRewind(LastRecordedTick - clamped);
            return found;
        }

        private List<(int Id, RewoundBody Body)> RewindBodies(float clamped, int shooterId, Func<int, bool> hittable)
        {
            var positions = _lag.Rewind(clamped);
            var crouched = _lag.RewindCrouched(clamped);
            var leans = _lag.RewindLean(clamped);
            _ids.Clear();
            foreach (var body in _bodies.All)
            {
                _ids.Add(body.PlayerId);
            }

            _rewound.Clear();
            foreach (var id in _ids)
            {
                if (id == shooterId || !positions.TryGetValue(id, out var position) || !hittable(id))
                {
                    continue;
                }

                var isCrouched = crouched.TryGetValue(id, out var c) && c;
                var lean = leans.TryGetValue(id, out var l) ? l : Vector3.Zero;
                _rewound.Add((id, Rewound(position, isCrouched, lean)));
            }

            return _rewound;
        }

        // 回す角度を MotorSpec.LeanAngleDegrees (見た目の体の角度) にしないのは、傾けた人の目 (カメラと撃つ位置) は LeanStep.EyeOffset だけずれるので、
        // 頭の当たりもその目の所へ合わせるため
        private RewoundBody Rewound(Vector3 position, bool crouched, Vector3 lean)
        {
            var geometry = _bodies.Geometry;
            var shape = geometry.Shape(crouched);
            geometry.SphereCenters(shape, out var bottomY, out var topY);
            var feetY = shape.CenterY - (shape.Height / 2f);
            var feet = position + new Vector3(0f, feetY, 0f);
            var up = Vector3.UnitY;
            var side = new Vector3(lean.X, 0f, lean.Z);
            var shift = side.Length();
            var eye = shape.EyeY - feetY;
            if (shift > 1e-6f && eye > 1e-4f)
            {
                var sin = Math.Min(shift / eye, 1f);
                up = (side / shift * sin) + (Vector3.UnitY * MathF.Sqrt(1f - (sin * sin)));
            }

            return new RewoundBody(
                position, crouched, shape, feet, up, feet + (up * (bottomY - feetY)), feet + (up * (topY - feetY)), geometry.Radius);
        }

        // 車体は人より手前で弾を止めるので、人の当たりより先に、世界の当たりと比べて近い方を残す。
        private bool NearerVehicle(Vector3 origin, Vector3 unit, float limit, float tick, int shooterId, ref ShotHit hit)
        {
            if (_vehicles == null
                || !_vehicles.Raycast(
                    origin, unit, limit, tick, shooterId, out var vehicle, out var distance, out var normal, out var multiplier, out var partId))
            {
                return false;
            }

            hit = ShotHit.Vehicle(origin + (unit * distance), normal, distance, vehicle, multiplier, partId);
            return true;
        }

        // 置いた物も .fpsmap に無いので、世界の当たりとは別に見る。撃った人の弾を通す物 (自分と味方の置いた物) は ServerDeployables.Raycast が飛ばす
        private bool NearerDeployable(Vector3 origin, Vector3 unit, float limit, int shooterId, ref ShotHit hit)
        {
            if (_deployables == null
                || !(limit > 0f)
                || !_deployables.Raycast(origin, unit, limit, shooterId, out var deployable, out var distance, out var normal))
            {
                return false;
            }

            hit = ShotHit.Deployable(origin + (unit * distance), normal, distance, deployable);
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

        private readonly struct RewoundBody
        {
            public RewoundBody(
                Vector3 position, bool crouched, StanceShape shape, Vector3 feet, Vector3 up, Vector3 bottom, Vector3 top, float radius)
            {
                Position = position;
                Crouched = crouched;
                Shape = shape;
                Feet = feet;
                Up = up;
                Bottom = bottom;
                Top = top;
                Radius = radius;
            }

            public Vector3 Position { get; }

            public bool Crouched { get; }

            public StanceShape Shape { get; }

            public Vector3 Feet { get; }

            public Vector3 Up { get; }

            public Vector3 Bottom { get; }

            public Vector3 Top { get; }

            public float Radius { get; }

            public Vector3 Center => Feet + (Up * (Shape.Height / 2f));

            // 傾けた体でも頭と足を分けられるよう、高さではなく体の軸に沿った足元からの長さで分ける
            public HitZone Zone(Vector3 point)
            {
                return ServerHitboxes.Zone(Vector3.Dot(point - Feet, Up), 0f, Shape.Height);
            }
        }
    }

    internal readonly struct ShotHit
    {
        private ShotHit(
            Vector3 point, Vector3 normal, float distance, bool hitTarget, int targetId, Vector3 targetPosition, bool targetCrouched,
            HitZone zone, int colliderId, int layer, float rewindTicks, int vehicle = -1, float damageMultiplier = 1f, string vehiclePartId = null,
            int deployable = 0, int? shieldHolder = null)
        {
            DeployableId = deployable;
            HitShield = shieldHolder.HasValue;
            ShieldHolderId = shieldHolder ?? PlayerIds.None;
            VehicleIndex = vehicle;
            DamageMultiplier = damageMultiplier;
            VehiclePartId = vehiclePartId;
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

        // 部位の付いていない車体の箱に当たったときは null。
        public string VehiclePartId { get; }

        // 置いた物の Id は 1 から振るので、0 は置いた物に当たっていない印
        public int DeployableId { get; }

        public bool HitDeployable => DeployableId > 0;

        // 持っている盾の板が止めた弾の、盾の持ち主。持ち主には当たっていないので、HitTarget は false のまま。
        // 当たったかを Id の値で見分けないのは、default の ShotHit (外れ) の Id 0 を盾に当たったと読まないため
        public int ShieldHolderId { get; }

        public bool HitShield { get; }

        public static ShotHit World(WorldHit hit)
        {
            return new ShotHit(hit.Point, hit.Normal, hit.Distance, false, 0, default, false, HitZone.Body, hit.ColliderId, hit.Layer, 0f);
        }

        public static ShotHit Target(
            Vector3 point, Vector3 normal, float distance, int targetId, Vector3 targetPosition, bool crouched, HitZone zone)
        {
            return new ShotHit(point, normal, distance, true, targetId, targetPosition, crouched, zone, -1, 0, 0f);
        }

        public static ShotHit Vehicle(Vector3 point, Vector3 normal, float distance, int vehicle, float damageMultiplier, string partId = null)
        {
            return new ShotHit(point, normal, distance, false, 0, default, false, HitZone.Body, -1, 0, 0f, vehicle, damageMultiplier, partId);
        }

        public static ShotHit Deployable(Vector3 point, Vector3 normal, float distance, int deployableId)
        {
            if (deployableId < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(deployableId), deployableId, "deployableId must be at least 1");
            }

            return new ShotHit(point, normal, distance, false, 0, default, false, HitZone.Body, -1, 0, 0f, deployable: deployableId);
        }

        public static ShotHit Shield(Vector3 point, Vector3 normal, float distance, int holderId)
        {
            if (holderId == PlayerIds.None)
            {
                throw new ArgumentOutOfRangeException(nameof(holderId), holderId, "holderId must name a player");
            }

            return new ShotHit(point, normal, distance, false, 0, default, false, HitZone.Body, -1, 0, 0f, shieldHolder: holderId);
        }

        public ShotHit WithRewind(float rewindTicks)
        {
            return new ShotHit(
                Point, Normal, Distance, HitTarget, TargetId, TargetPosition, TargetCrouched, Zone, ColliderId, Layer, rewindTicks, VehicleIndex,
                DamageMultiplier, VehiclePartId, DeployableId, HitShield ? ShieldHolderId : null);
        }

        // 跳ねた弾・飛ぶ弾は距離減衰を進んだ道のりで決めるので、当たった物はそのままに距離だけを替える
        public ShotHit WithDistance(float distance)
        {
            return new ShotHit(
                Point, Normal, distance, HitTarget, TargetId, TargetPosition, TargetCrouched, Zone, ColliderId, Layer, RewindTicks, VehicleIndex,
                DamageMultiplier, VehiclePartId, DeployableId, HitShield ? ShieldHolderId : null);
        }
    }
}

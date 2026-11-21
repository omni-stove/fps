using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Unity の RemoteChargeDeployDefinition.Survey と RemoteCharge.DetonateAll と同じく、狙った先の面 (マップ・人の体・乗り物) にそのまま貼り、
    // 起爆の要求でその人の貼った同じ種類を全部爆発させる。人や乗り物に貼った物は、貼った先に付いて動く (ServerDeployables.Anchor)
    internal sealed partial class ServerDeployables
    {
        // 送られた地点と、体の中心から引いた線が面に当たった所とのずれをこれだけ許す。持ち主の画面の位置はサーバーより遅れているため
        public const float SurfaceTolerance = 0.15f;

        private readonly List<Placed> _detonating = new List<Placed>();
        private readonly List<Vector3> _blastCenters = new List<Vector3>();

        private enum SurfaceKind
        {
            World,
            Body,
            Vehicle,
        }

        internal ServerDeployRefusal Detonate(int playerId, string typeId)
        {
            if (_data.Rules?.Actions.Abilities != Abilities.On)
            {
                return Refuse(playerId, null, ServerDeployRefusal.RuleOff);
            }

            if (!_rules.CanFire(playerId) || !_bodies.TryGet(playerId, out var body) || !body.HasSpawned)
            {
                return Refuse(playerId, null, ServerDeployRefusal.CannotAct);
            }

            if (!_catalog.TryGet(typeId, out var type))
            {
                return Refuse(playerId, null, ServerDeployRefusal.UnknownType);
            }

            if (type.Role != DeployRole.RemoteCharge)
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.NotDetonatable);
            }

            // 持ち替えた後でも、もう貼ってある物は持ち主のものなので、能力を今持っているかは見ない
            _detonating.Clear();
            if (_rosters.TryGetValue((playerId, type.Id), out var roster))
            {
                _detonating.AddRange(roster.Items);
            }

            if (_detonating.Count == 0)
            {
                return Refuse(playerId, type.Id, ServerDeployRefusal.NothingToDetonate);
            }

            var owner = ServerDeployableLog.Owner(playerId);
            _log.WriteLine(RemoteChargeLog.Detonating(owner, type.Id, _detonating.Count));

            // 先にすべて消してから爆発させるのは、隣の C4 が先の爆発で「壊された」として消え、爆発しなくなるのを防ぐため (Unity の RemoteCharge.DetonateAll と同じ)
            _blastCenters.Clear();
            foreach (var placed in _detonating)
            {
                // この tick の置いた物の動きより後に貼った先が動いても、今の所で爆発させる (Unity の RemoteCharge.DetonateAll も先に Follow する)
                Follow(placed);
                var center = BlastCenterOf(placed);
                _blastCenters.Add(center);
                _log.WriteLine(RemoteChargeLog.Detonated(owner, type.Id, placed.Id, center));
                Remove(placed, DeployRemoval.Detonated);
            }

            var explosion = type.RemoteCharge.Explosion;
            for (var i = 0; i < _blastCenters.Count; i++)
            {
                _combat.Explode(_blastCenters[i], explosion, playerId, type.Id);
            }

            _detonating.Clear();
            _blastCenters.Clear();
            return ServerDeployRefusal.None;
        }

        // 体の中心から送られた地点へ線を引き、その手前の少し先までで最初に当たった面に貼る。
        // 手前で壁に当たれば届かない、何にも当たらなければ貼る面が無い、置いた物に先に当たれば塞がれているとして断る
        // (Unity の Survey も置いた物には貼らない。盾ごと壊れて爆発しなくなるため)
        private ServerDeployRefusal Attach(
            ServerBody body, Vector3 position, float yaw, out Vector3 point, out Vector3 normal, out Anchor anchor, out string surfaceLabel)
        {
            point = position;
            normal = Vector3.UnitY;
            anchor = null;
            surfaceLabel = null;
            var from = body.Center;
            var offset = position - from;
            var distance = offset.Length();
            if (distance < 1e-4f)
            {
                return ServerDeployRefusal.NoGround;
            }

            var unit = offset / distance;
            if (!TryFindSurface(body.PlayerId, from, unit, distance + SurfaceTolerance, out var surface))
            {
                return ServerDeployRefusal.NoGround;
            }

            if (surface.Distance < distance - SurfaceTolerance)
            {
                return ServerDeployRefusal.OutOfReach;
            }

            // 面の向きが壊れた当たり (縮退した三角形など) で例外にするより、貼る面が無いとして断る
            if (!IsFinite(surface.Normal) || surface.Normal.LengthSquared() < 1e-12f)
            {
                return ServerDeployRefusal.NoGround;
            }

            if (Raycast(from, unit, surface.Distance, null, null, out _, out _, out _))
            {
                return ServerDeployRefusal.Blocked;
            }

            var hitPoint = surface.Point;
            var hitNormal = Vector3.Normalize(surface.Normal);
            switch (surface.Kind)
            {
                case SurfaceKind.Body:
                    if (!_bodies.TryGet(surface.Id, out var target))
                    {
                        return ServerDeployRefusal.NoGround;
                    }

                    surfaceLabel = ServerDeployableLog.Body(surface.Id);
                    anchor = AnchorOn(AnchorKind.Body, surface.Id, surfaceLabel, target.Position, YawRotation(target.Yaw), hitPoint, hitNormal, yaw);
                    break;
                case SurfaceKind.Vehicle:
                    var vehicles = _combat.Vehicles;
                    if (vehicles == null || !vehicles.TryGetState(surface.Id, out var carPosition, out var carRotation, out _, out _))
                    {
                        return ServerDeployRefusal.NoGround;
                    }

                    surfaceLabel = ServerDeployableLog.Vehicle(vehicles.NameOf(surface.Id));
                    anchor = AnchorOn(AnchorKind.Vehicle, surface.Id, surfaceLabel, carPosition, carRotation, hitPoint, hitNormal, yaw);
                    break;
                default:
                    surfaceLabel = ServerDeployableLog.Surface(surface.Id);
                    break;
            }

            point = hitPoint;
            normal = hitNormal;
            return ServerDeployRefusal.None;
        }

        // 人の体は撃った弾と違い巻き戻さず、今の姿で測る。貼った後は今の体に付いて動くので、貼る所も今の体の面にそろえる。
        // 持ち主の体は飛ばす (Unity の Survey も持ち主のコライダーを飛ばす)
        private bool TryFindSurface(int ownerId, Vector3 from, Vector3 unit, float limit, out Surface surface)
        {
            surface = default;
            var found = false;
            if (_context.World.Raycast(from, unit, limit, ServerHitboxes.RaycastLayers, out var hit))
            {
                surface = new Surface(SurfaceKind.World, hit.ColliderId, hit.Distance, hit.Point, hit.Normal);
                found = true;
            }

            var vehicles = _combat.Vehicles;
            if (vehicles != null
                && vehicles.RaycastPresent(from, unit, found ? surface.Distance : limit, ownerId, out var vehicle, out var carDistance, out var carNormal)
                && (!found || carDistance < surface.Distance))
            {
                surface = new Surface(SurfaceKind.Vehicle, vehicle, carDistance, from + (unit * carDistance), carNormal);
                found = true;
            }

            foreach (var other in _bodies.All)
            {
                var id = other.PlayerId;
                if (id == ownerId || !other.HasSpawned || !_combat.IsHittable(id))
                {
                    continue;
                }

                other.Capsule(out var bottom, out var top);
                var reach = found ? surface.Distance : limit;
                if (ServerHitboxes.RayCapsule(from, unit, bottom, top, other.Radius, reach, out var bodyDistance, out var bodyNormal)
                    && (!found || bodyDistance < surface.Distance))
                {
                    surface = new Surface(SurfaceKind.Body, id, bodyDistance, from + (unit * bodyDistance), bodyNormal);
                    found = true;
                }
            }

            return found;
        }

        // Id はマップのコライダーの番号・人の Id・乗り物の番号のどれか (Kind で決まる)
        private readonly struct Surface
        {
            public Surface(SurfaceKind kind, int id, float distance, Vector3 point, Vector3 normal)
            {
                Kind = kind;
                Id = id;
                Distance = distance;
                Point = point;
                Normal = normal;
            }

            public SurfaceKind Kind { get; }

            public int Id { get; }

            public float Distance { get; }

            public Vector3 Point { get; }

            public Vector3 Normal { get; }
        }
    }
}

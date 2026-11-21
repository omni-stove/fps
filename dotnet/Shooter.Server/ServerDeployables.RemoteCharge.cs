using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // Unity の RemoteChargeDeployDefinition.Survey と RemoteCharge.DetonateAll と同じく、狙った先の面にそのまま貼り、
    // 起爆の要求でその人の貼った同じ種類を全部爆発させる。貼る先はマップの当たりだけで、人や乗り物には貼らない (サーバーは付いていく先を持たない)
    internal sealed partial class ServerDeployables
    {
        // 送られた地点と、体の中心から引いた線が面に当たった所とのずれをこれだけ許す。持ち主の画面の位置はサーバーより遅れているため
        public const float SurfaceTolerance = 0.15f;

        private readonly List<Placed> _detonating = new List<Placed>();
        private readonly List<Vector3> _blastCenters = new List<Vector3>();

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

        // 体の中心から送られた地点へ線を引き、その手前の少し先までで最初に当たったマップの面に貼る。
        // 手前で壁に当たれば届かない、何にも当たらなければ貼る面が無い、置いた物に先に当たれば塞がれているとして断る
        // (Unity の Survey も置いた物には貼らない。盾ごと壊れて爆発しなくなるため)
        private ServerDeployRefusal Attach(ServerBody body, Vector3 position, out Vector3 point, out Vector3 normal, out int surfaceId)
        {
            point = position;
            normal = Vector3.UnitY;
            surfaceId = 0;
            var from = body.Center;
            var offset = position - from;
            var distance = offset.Length();
            if (distance < 1e-4f)
            {
                return ServerDeployRefusal.NoGround;
            }

            var unit = offset / distance;
            if (!_context.World.Raycast(from, unit, distance + SurfaceTolerance, ServerHitboxes.RaycastLayers, out var hit))
            {
                return ServerDeployRefusal.NoGround;
            }

            if (hit.Distance < distance - SurfaceTolerance)
            {
                return ServerDeployRefusal.OutOfReach;
            }

            // 面の向きが壊れた当たり (縮退した三角形など) で例外にするより、貼る面が無いとして断る
            var surface = hit.Normal;
            if (!IsFinite(surface) || surface.LengthSquared() < 1e-12f)
            {
                return ServerDeployRefusal.NoGround;
            }

            if (Raycast(from, unit, hit.Distance, null, null, out _, out _, out _))
            {
                return ServerDeployRefusal.Blocked;
            }

            point = hit.Point;
            normal = Vector3.Normalize(surface);
            surfaceId = hit.ColliderId;
            return ServerDeployRefusal.None;
        }
    }
}

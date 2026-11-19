using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // 戦闘の爆発と Bot の知覚が置いた物を見る口。弾の当たりは Raycast と Damage (ServerDeployables.cs) が受ける
    internal sealed partial class ServerDeployables
    {
        private readonly List<Placed> _blasted = new List<Placed>();

        // 爆風は Damage を通すので、投げた人と味方の置いた物は弾と同じく傷まない。投げた人が抜けていれば、どの置いた物にも届く。
        // 遮りは世界の物と、ほかの置いた物で見る (オフラインの Explosion.IsLineObstructed は自分以外の置いた物を壁として数える)
        internal int ApplyExplosion(Vector3 center, ExplosionDamage explosion, int? attackerId)
        {
            ArgumentNullException.ThrowIfNull(explosion);
            if (_placed.Count == 0 || _context == null)
            {
                return 0;
            }

            // 壊れて一覧から外れる物があっても、同じ爆発の遮りは爆発した時の並びで見るよう、写してから回す
            _blasted.Clear();
            _blasted.AddRange(_placed.Values);
            var damaged = 0;
            foreach (var placed in _blasted)
            {
                if (placed.Removed)
                {
                    continue;
                }

                var spec = placed.Type.Deploy;
                var distance = DistanceToBox(center, placed.Position, placed.Yaw, spec);
                if (!explosion.Reaches(distance))
                {
                    continue;
                }

                var target = CenterOf(placed.Position, spec);
                var obstructed = IsBlastBlocked(center, target, placed.Id);
                var amount = explosion.At(distance, obstructed);
                if (amount > 0 && Damage(placed.Id, attackerId, amount))
                {
                    damaged++;
                }
            }

            _blasted.Clear();
            return damaged;
        }

        // 置いた盾は壁として置かれる物なので、誰の物でも爆風を遮る (オフラインの Explosion.Blocks と同じ)
        internal bool BlocksLine(Vector3 from, Vector3 to, int? skipId)
        {
            var offset = to - from;
            var length = offset.Length();
            return length >= 1e-4f && Raycast(from, offset / length, length, null, skipId, out _, out _, out _);
        }

        // 囮だけを Bot の知覚へ渡す。囮は陣営の参加者ではないので、Bot の目標の候補には入れず、知覚の Target だけを差し替えさせる
        internal void CollectDecoys(List<ServerDecoy> decoys)
        {
            ArgumentNullException.ThrowIfNull(decoys);
            decoys.Clear();
            foreach (var placed in _placed.Values)
            {
                if (placed.Decoy == null)
                {
                    continue;
                }

                decoys.Add(new ServerDecoy(
                    placed.Id,
                    placed.OwnerId,
                    placed.Type.Id,
                    placed.Team,
                    placed.Position,
                    CenterOf(placed.Position, placed.Type.Deploy),
                    placed.Decoy.Calls,
                    placed.Type.Decoy.CallRadius));
            }
        }

        private bool IsBlastBlocked(Vector3 center, Vector3 target, int targetId)
        {
            var offset = target - center;
            var length = offset.Length();
            if (length < 1e-4f)
            {
                return false;
            }

            return _context.World.Raycast(center, offset / length, length, ServerHitboxes.RaycastLayers, out _)
                || Raycast(center, offset / length, length, null, targetId, out _, out _, out _);
        }
    }

    // 囮 1 つの、Bot が見て選ぶのに要る値。位置は足元 (オフラインの Damageable.GroundPoint)、見えるかは体の中ほど (AimPoint) で見る
    internal readonly struct ServerDecoy
    {
        public ServerDecoy(int id, int ownerId, string typeId, Team team, Vector3 position, Vector3 aimPoint, int calls, float callRadius)
        {
            Id = id;
            OwnerId = ownerId;
            TypeId = typeId;
            Team = team;
            Position = position;
            AimPoint = aimPoint;
            Calls = calls;
            CallRadius = callRadius;
        }

        public int Id { get; }

        public int OwnerId { get; }

        public string TypeId { get; }

        public Team Team { get; }

        public Vector3 Position { get; }

        public Vector3 AimPoint { get; }

        public int Calls { get; }

        public float CallRadius { get; }
    }
}

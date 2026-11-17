using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // ネット対戦の相手は、最新のスナップショットで生きている自分以外のプレイヤー。狙う点は画面に出している (補間した) 姿の体の中ほどにし、
    // サーバーも撃った人が見ていた tick の体の中ほどで確かめる (ServerLockOn)。
    public sealed class ClientLockOnTargets : ILockOnTargets
    {
        // サーバーの ServerHitboxes.RaycastLayers と同じく Ignore Raycast (2) だけを外す。
        public const int SightLayers = ~(1 << 2);

        private readonly int _localPlayerId;
        private readonly Func<SnapshotMessage> _latest;
        private readonly TrySampleRemote _sample;
        private readonly IWorldCollision _world;
        private readonly StanceGeometry _geometry;

        public ClientLockOnTargets(
            int localPlayerId, Func<SnapshotMessage> latest, TrySampleRemote sample, IWorldCollision world, StanceGeometry geometry)
        {
            _localPlayerId = localPlayerId;
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
            _sample = sample ?? throw new ArgumentNullException(nameof(sample));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
        }

        public delegate bool TrySampleRemote(int playerId, out RemotePose pose);

        public void Collect(Vector3 eye, Vector3 forward, float range, List<LockOnCandidate> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            var latest = _latest();
            if (latest == null)
            {
                return;
            }

            for (var i = 0; i < latest.Players.Count; i++)
            {
                var row = latest.Players[i];
                if (row.PlayerId == _localPlayerId || row.Health <= 0 || !TryLocate(row.PlayerId, out var point))
                {
                    continue;
                }

                var offset = point - eye;
                var distance = offset.Length();
                if (distance > range || (distance > 1e-4f && _world.Raycast(eye, offset / distance, distance, SightLayers, out _)))
                {
                    continue;
                }

                into.Add(new LockOnCandidate(row.PlayerId, point));
            }
        }

        public Func<Vector3?> Follow(int id)
        {
            return () => TryLocate(id, out var point) ? point : (Vector3?)null;
        }

        private bool TryLocate(int playerId, out Vector3 point)
        {
            point = default;
            if (!_sample(playerId, out var pose))
            {
                return false;
            }

            point = pose.Position + new Vector3(0f, _geometry.Shape(pose.Stance).CenterY, 0f);
            return true;
        }
    }
}

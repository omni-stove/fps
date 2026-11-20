using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 狙う点と見通しの確かめ方はロックオン (ClientLockOnTargets) と揃え、吸い付く先とロックできる相手が食い違わないようにする。
    // 陣営は試合の途中で振り直されることがあるので、自分のチームも毎回最新のスナップショットから引き直す。
    public sealed class ClientHostileAimTargets : IAimAssistTargets
    {
        private readonly int _localPlayerId;
        private readonly Func<SnapshotMessage> _latest;
        private readonly ClientLockOnTargets.TrySampleRemote _sample;
        private readonly IWorldCollision _world;
        private readonly StanceGeometry _geometry;
        private readonly Func<Vector3, Vector3, bool> _visionBlocked;

        public ClientHostileAimTargets(
            int localPlayerId,
            Func<SnapshotMessage> latest,
            ClientLockOnTargets.TrySampleRemote sample,
            IWorldCollision world,
            StanceGeometry geometry,
            Func<Vector3, Vector3, bool> visionBlocked = null)
        {
            _localPlayerId = localPlayerId;
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
            _sample = sample ?? throw new ArgumentNullException(nameof(sample));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _visionBlocked = visionBlocked;
        }

        public void Collect(Vector3 eye, float range, List<Vector3> into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            if (!(range > 0f) || float.IsInfinity(range))
            {
                throw new ArgumentOutOfRangeException(nameof(range), range, "range must be positive and finite");
            }

            var latest = _latest();
            if (latest == null)
            {
                return;
            }

            // 自分のチームが分からない間は誰が味方か決められないので、味方へ吸い付かないよう何も集めない。
            var own = latest.Players.IndexOf(_localPlayerId);
            if (own < 0)
            {
                return;
            }

            var team = latest.Players[own].Team;
            for (var i = 0; i < latest.Players.Count; i++)
            {
                var row = latest.Players[i];
                if (row.PlayerId == _localPlayerId || row.Team == team || row.Concealed || row.Health <= 0 || row.Downed.IsDowned)
                {
                    continue;
                }

                if (!_sample(row.PlayerId, out var pose))
                {
                    continue;
                }

                var point = pose.Position + new Vector3(0f, _geometry.Shape(pose.Stance).CenterY, 0f);
                if (IsInSight(eye, point, range))
                {
                    into.Add(point);
                }
            }
        }

        private bool IsInSight(Vector3 eye, Vector3 point, float range)
        {
            var offset = point - eye;
            var distance = offset.Length();
            if (distance > range)
            {
                return false;
            }

            if (distance > 1e-4f && _world.Raycast(eye, offset / distance, distance, ClientLockOnTargets.SightLayers, out _))
            {
                return false;
            }

            return _visionBlocked == null || !_visionBlocked(eye, point);
        }
    }
}

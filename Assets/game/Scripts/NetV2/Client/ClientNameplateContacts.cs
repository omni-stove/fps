using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 名前を出すかは NameplateRule に任せ、ここは名簿 (最新のスナップショット) と画面に出している (補間した) 姿から相手を集めるだけにする。
    // 見通しはロックオン (ClientLockOnTargets) と同じ体の中ほどへの線で確かめ、名前が見える相手と狙える相手が食い違わないようにする。
    // 陣営は試合の途中で振り直されることがあるので、自分のチームも毎回最新のスナップショットから引き直す。
    public sealed class ClientNameplateContacts : INameplateContacts
    {
        private readonly int _localPlayerId;
        private readonly Func<SnapshotMessage> _latest;
        private readonly ClientLockOnTargets.TrySampleRemote _sample;
        private readonly IWorldCollision _world;
        private readonly StanceGeometry _geometry;
        private readonly Func<Vector3, Vector3, bool> _visionBlocked;

        public ClientNameplateContacts(
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

        public void CopyTo(Vector3 eye, List<NameplateContact> into)
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

            // 自分の行が無い間は誰が味方か決められないので、オフライン (OfflineNameplateTargets) と同じく全員を敵として壁越しに名前を出さない。
            var own = latest.Players.IndexOf(_localPlayerId);
            var hasTeam = own >= 0;
            var team = hasTeam ? latest.Players[own].Team : default;
            for (var i = 0; i < latest.Players.Count; i++)
            {
                var row = latest.Players[i];
                if (row.PlayerId == _localPlayerId || row.Concealed || !_sample(row.PlayerId, out var pose) || !IsFinite(pose.Position))
                {
                    continue;
                }

                var shape = _geometry.Shape(pose.Stance);
                var relation = hasTeam && row.Team == team ? TeamRelation.Friendly : TeamRelation.Enemy;
                var alive = row.Health > 0;

                // 味方は壁越しでも出すので、遮る物を調べるのは生きている敵だけにする。
                var inSight = relation == TeamRelation.Enemy
                    && alive
                    && IsInSight(eye, pose.Position + new Vector3(0f, shape.CenterY, 0f));
                into.Add(new NameplateContact(row.PlayerId, pose.Position + new Vector3(0f, shape.TopY, 0f), relation, alive, inSight));
            }
        }

        private bool IsInSight(Vector3 eye, Vector3 point)
        {
            if (!IsFinite(eye))
            {
                return false;
            }

            var offset = point - eye;
            var distance = offset.Length();
            if (distance > 1e-4f && _world.Raycast(eye, offset / distance, distance, ClientLockOnTargets.SightLayers, out _))
            {
                return false;
            }

            return _visionBlocked == null || !_visionBlocked(eye, point);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}

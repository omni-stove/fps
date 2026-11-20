using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // 頭の位置はスナップショットの位置でなく補間した姿 (相手の体を置くのと同じ値) から取る。スナップショットの位置では名前が体より先に動く。
    // 陣営は試合の途中で振り直されることがあるので、ClientHostileAimTargets と同じく自分のチームも毎回最新のスナップショットから引き直す。
    public sealed class NetV2NameplateTargets : INameplateTargets
    {
        private readonly int _localPlayerId;
        private readonly Func<SnapshotMessage> _latest;
        private readonly ClientLockOnTargets.TrySampleRemote _sample;
        private readonly IWorldCollision _world;
        private readonly StanceGeometry _geometry;
        private readonly Func<IParticipants> _participants;
        private readonly Func<System.Numerics.Vector3, System.Numerics.Vector3, bool> _visionBlocked;

        // 名前の表 (NetV2Hud の名簿) はマップごとに作り直されるので、値ではなく引く口を受け取る。
        public NetV2NameplateTargets(
            int localPlayerId,
            Func<SnapshotMessage> latest,
            ClientLockOnTargets.TrySampleRemote sample,
            IWorldCollision world,
            StanceGeometry geometry,
            Func<IParticipants> participants,
            Func<System.Numerics.Vector3, System.Numerics.Vector3, bool> visionBlocked = null)
        {
            _localPlayerId = localPlayerId;
            _latest = latest ?? throw new ArgumentNullException(nameof(latest));
            _sample = sample ?? throw new ArgumentNullException(nameof(sample));
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            _participants = participants ?? throw new ArgumentNullException(nameof(participants));
            _visionBlocked = visionBlocked;
        }

        public IParticipants Participants => _participants();

        public void CopyTo(UnityEngine.Vector3 eye, List<NameplateTarget> targets)
        {
            if (targets == null)
            {
                throw new ArgumentNullException(nameof(targets));
            }

            var latest = _latest();
            if (latest == null)
            {
                return;
            }

            // 自分のチームが分からない間は誰が味方か決められず、敵を味方として壁越しに出しかねないので何も出さない。
            var own = latest.Players.IndexOf(_localPlayerId);
            if (own < 0)
            {
                return;
            }

            var team = latest.Players[own].Team;
            var from = NetV2Math.ToNumerics(eye);
            var eyeIsFinite = IsFinite(from);
            for (var i = 0; i < latest.Players.Count; i++)
            {
                var row = latest.Players[i];
                if (row.PlayerId == _localPlayerId || !_sample(row.PlayerId, out var pose) || !IsFinite(pose.Position))
                {
                    continue;
                }

                var shape = _geometry.Shape(pose.Stance);
                var head = pose.Position + new System.Numerics.Vector3(0f, shape.TopY, 0f);
                var relation = row.Team == team ? TeamRelation.Friendly : TeamRelation.Enemy;
                var alive = row.Health > 0;

                // 味方は壁越しでも出すので、遮る物を調べるのは生きている敵だけにする。狙う点はロックオンと同じ体の中心。
                var inSight = relation == TeamRelation.Enemy
                    && alive
                    && eyeIsFinite
                    && IsInSight(from, pose.Position + new System.Numerics.Vector3(0f, shape.CenterY, 0f));
                targets.Add(new NameplateTarget(row.PlayerId, NetV2Math.ToUnity(head), relation, alive, inSight));
            }
        }

        private bool IsInSight(System.Numerics.Vector3 eye, System.Numerics.Vector3 point)
        {
            var offset = point - eye;
            var distance = offset.Length();
            if (distance > 1e-4f && _world.Raycast(eye, offset / distance, distance, ClientLockOnTargets.SightLayers, out _))
            {
                return false;
            }

            return _visionBlocked == null || !_visionBlocked(eye, point);
        }

        private static bool IsFinite(System.Numerics.Vector3 value)
        {
            return !float.IsNaN(value.X) && !float.IsInfinity(value.X)
                && !float.IsNaN(value.Y) && !float.IsInfinity(value.Y)
                && !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
        }
    }
}

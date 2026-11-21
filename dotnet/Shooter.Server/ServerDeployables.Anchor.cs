using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Server
{
    // 人や乗り物に貼った C4 を、毎 tick 貼った先の今の姿に合わせて動かす (Unity の RemoteCharge.Follow と同じく、貼ったときの相対の位置と向きを保つ)。
    // 貼った先を失った物は、最後に合わせた所に残す (Unity でも付いていく先が消えると、その場に残る)
    internal sealed partial class ServerDeployables
    {
        private readonly List<Placed> _released = new List<Placed>();

        private enum AnchorKind
        {
            Body,
            Vehicle,
        }

        // 体は上から見た向きだけで回す。サーバーの体は傾かず、Unity の CharacterController も Y 軸まわりにしか回らないため
        private static Quaternion YawRotation(float yawDegrees)
        {
            return Quaternion.CreateFromAxisAngle(Vector3.UnitY, yawDegrees * (MathF.PI / 180f));
        }

        private static Anchor AnchorOn(
            AnchorKind kind, int targetId, string label, Vector3 origin, Quaternion rotation, Vector3 point, Vector3 normal, float yaw)
        {
            var inverse = Quaternion.Inverse(rotation);
            return new Anchor(
                kind,
                targetId,
                label,
                Vector3.Transform(point - origin, inverse),
                Vector3.Transform(normal, inverse),
                Vector3.Transform(Forward(yaw), inverse));
        }

        private void Follow(Placed placed)
        {
            var anchor = placed.Anchor;
            if (anchor == null)
            {
                return;
            }

            var loss = FrameOf(anchor, out var origin, out var rotation);
            if (loss.HasValue)
            {
                Release(placed, loss.Value);
                return;
            }

            placed.Position = origin + Vector3.Transform(anchor.LocalPoint, rotation);
            placed.SurfaceNormal = Vector3.Normalize(Vector3.Transform(anchor.LocalNormal, rotation));

            // 前が真上か真下を向いた (乗り物が縦に立った) ときは上から見た向きが決まらないので、前の向きのままにする
            var forward = Vector3.Transform(anchor.LocalForward, rotation);
            if ((forward.X * forward.X) + (forward.Z * forward.Z) > 1e-8f)
            {
                placed.Yaw = NormalizeYaw(MathF.Atan2(forward.X, forward.Z) * (180f / MathF.PI));
            }
        }

        private ServerAnchorLoss? FrameOf(Anchor anchor, out Vector3 origin, out Quaternion rotation)
        {
            origin = default;
            rotation = Quaternion.Identity;
            if (anchor.Kind == AnchorKind.Vehicle)
            {
                var vehicles = _combat.Vehicles;
                return vehicles != null && vehicles.TryGetState(anchor.TargetId, out origin, out rotation, out _, out _)
                    ? (ServerAnchorLoss?)null
                    : ServerAnchorLoss.Gone;
            }

            var session = _rules.Session;
            if (session == null || !session.HasPlayer(anchor.TargetId) || !_bodies.TryGet(anchor.TargetId, out var body) || !body.HasSpawned)
            {
                return ServerAnchorLoss.Gone;
            }

            if (!session.IsAlive(anchor.TargetId))
            {
                return ServerAnchorLoss.Died;
            }

            origin = body.Position;
            rotation = YawRotation(body.Yaw);
            return null;
        }

        private void Release(Placed placed, ServerAnchorLoss loss)
        {
            var anchor = placed.Anchor;
            if (anchor == null)
            {
                return;
            }

            placed.Anchor = null;
            _log.WriteLine(ServerDeployableLog.Released(placed.OwnerId, placed.Type.Id, placed.Id, anchor.Label, loss));
        }

        // 出現し直した人や抜けた人に付いていくと、出現地点へ飛んだり消えた体の所で止まったりするので、その前に付いていく先から外す
        private void ReleaseFrom(int playerId, ServerAnchorLoss loss)
        {
            _released.Clear();
            foreach (var placed in _placed.Values)
            {
                if (placed.Anchor != null && placed.Anchor.Kind == AnchorKind.Body && placed.Anchor.TargetId == playerId)
                {
                    _released.Add(placed);
                }
            }

            foreach (var placed in _released)
            {
                Release(placed, loss);
            }

            _released.Clear();
        }

        // 貼った先から見た位置・面の向き・前の向き。貼った先の今の姿を掛けると世界の値に戻る
        private sealed class Anchor
        {
            public Anchor(AnchorKind kind, int targetId, string label, Vector3 localPoint, Vector3 localNormal, Vector3 localForward)
            {
                Kind = kind;
                TargetId = targetId;
                Label = label;
                LocalPoint = localPoint;
                LocalNormal = localNormal;
                LocalForward = localForward;
            }

            public AnchorKind Kind { get; }

            // 体なら人の Id、乗り物ならマップの乗り物の番号
            public int TargetId { get; }

            public string Label { get; }

            public Vector3 LocalPoint { get; }

            public Vector3 LocalNormal { get; }

            public Vector3 LocalForward { get; }
        }
    }
}

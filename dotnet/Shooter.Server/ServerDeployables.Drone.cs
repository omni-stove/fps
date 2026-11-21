using System;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 持ち主の送る操縦 (DroneControlMessage) で飛ばし、電池と届く距離はオフラインと同じ Core (DroneState) で数える。
    // 位置と向きは置いた物の行 (DeployableKind.Drone) に載るので、ほかの人にも飛んで見える。
    // 見回して敵に印を付けるのはオフラインだけで、ここでは飛ぶ・落ちる・戻すだけを持つ
    internal sealed partial class ServerDeployables
    {
        // クライアントは操縦の間これより短い間隔で送り直す (ClientDrone.ResendSeconds)。落ちた送信が続いても、届いた最後の入力で飛び続けないよう止める
        public const float DroneInputHoldSeconds = 0.3f;

        // Unity の ReconDrone.WallSkin・MinCollisionRadius と同じ値で止める
        public const float DroneWallSkin = 0.02f;

        public const float DroneMinCollisionRadius = 0.05f;

        internal ServerDroneControlRefusal Steer(int playerId, DroneControlMessage control)
        {
            ArgumentNullException.ThrowIfNull(control);
            if (!_placed.TryGetValue(control.Deployable, out var placed))
            {
                return RefuseControl(playerId, null, control.Deployable, ServerDroneControlRefusal.NotFound);
            }

            if (placed.Drone == null)
            {
                return RefuseControl(playerId, null, control.Deployable, ServerDroneControlRefusal.NotDrone);
            }

            if (placed.OwnerId != playerId)
            {
                return RefuseControl(playerId, null, control.Deployable, ServerDroneControlRefusal.NotOwner);
            }

            // 戻すのは確実に届く経路で送られ、操縦の番号より後に着くことがあるので、番号を見ずに受ける
            if (control.Recall)
            {
                _log.WriteLine(DroneLog.Recalled(ServerDeployableLog.Owner(playerId), placed.Type.Id, placed.Id, placed.Position));
                Remove(placed, DeployRemoval.Recalled);
                return ServerDroneControlRefusal.None;
            }

            var flight = placed.Drone;
            if (control.Sequence <= flight.LastSequence)
            {
                return ServerDroneControlRefusal.Stale;
            }

            flight.LastSequence = control.Sequence;

            // 倒れた人は操縦できないので、届いた入力を捨てて止める (オフラインの DroneControl は倒れると降りる)
            if (!_rules.CanFire(playerId))
            {
                flight.Stop();
                return RefuseControl(playerId, placed.Type.Id, placed.Id, ServerDroneControlRefusal.CannotAct);
            }

            var move = control.Move;
            var length = move.Length();
            flight.Move = length > 1f ? move / length : move;
            flight.Climb = Math.Clamp(control.Climb, -1f, 1f);
            flight.InputAt = _now;
            placed.Yaw = NormalizeYaw(control.Yaw);
            return ServerDroneControlRefusal.None;
        }

        internal float DroneBatterySecondsLeft(int deployableId)
        {
            return _placed.TryGetValue(deployableId, out var placed) && placed.Drone != null ? placed.Drone.State.BatterySecondsLeft : 0f;
        }

        private void TickDrone(Placed placed, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                return;
            }

            var flight = placed.Drone;
            if (flight.HasInput && (_now - flight.InputAt > DroneInputHoldSeconds || !_rules.CanFire(placed.OwnerId)))
            {
                flight.Stop();
            }

            if (flight.HasInput)
            {
                Fly(placed, flight, deltaSeconds);
            }

            flight.State.Tick(deltaSeconds, Vector3.Distance(placed.Position, flight.Launch));
            var over = flight.State.Over;
            if (over.HasValue)
            {
                Remove(placed, over.Value);
            }
        }

        // 向きはドローンの向きで、前後左右は水平に、上下は真上へ動かす (オフラインの DroneControl.Step と同じ)。
        // 壁の手前で止めるだけで滑らせないのも、オフラインの ReconDrone.Fly と同じ
        private void Fly(Placed placed, DroneFlight flight, float deltaSeconds)
        {
            var direction = (Right(placed.Yaw) * flight.Move.X) + (Forward(placed.Yaw) * flight.Move.Y) + (Vector3.UnitY * flight.Climb);
            var length = direction.Length();
            if (length > 1f)
            {
                direction /= length;
            }

            var step = direction * (flight.Spec.Speed * deltaSeconds);
            var distance = step.Length();
            if (!(distance > 1e-6f))
            {
                return;
            }

            var heading = step / distance;
            var radius = DroneCollisionRadius(placed.Type.Deploy);

            // 床に置いた物の当たりの球は床に接しているので、そのまま掃くと「動かす前から重なっている」として床を外し、下へすり抜けてしまう。
            // 球を壁の手前で止める幅だけ浮かせて、床にも止まるようにする
            var center = placed.Position + new Vector3(0f, radius + DroneWallSkin, 0f);
            var allowed = distance;
            if (_context.World.CapsuleCast(center, center, radius, heading, distance + DroneWallSkin, ServerHitboxes.RaycastLayers, out var hit))
            {
                allowed = Math.Min(distance, hit.Distance - DroneWallSkin);
            }

            if (allowed > 0f)
            {
                placed.Position += heading * allowed;
            }
        }

        // Unity の DroneDeployDefinition.CollisionRadiusOf と同じく、いちばん狭い辺を直径にする
        private static float DroneCollisionRadius(DeploySpec spec)
        {
            return Math.Max(DroneMinCollisionRadius, Math.Min(spec.Height, Math.Min(spec.Width, spec.Depth)) / 2f);
        }

        private ServerDroneControlRefusal RefuseControl(int playerId, string knownTypeId, int deployableId, ServerDroneControlRefusal reason)
        {
            if (CanWarn())
            {
                _log.WriteLine(ServerDeployableLog.DroneControlRefused(playerId, knownTypeId, deployableId, reason));
            }

            return reason;
        }

        private sealed class DroneFlight
        {
            public DroneFlight(DroneSpec spec, Vector3 launch)
            {
                Spec = spec ?? throw new ArgumentNullException(nameof(spec));
                State = new DroneState(spec);
                Launch = launch;
            }

            public DroneSpec Spec { get; }

            public DroneState State { get; }

            // 届く距離は置いた所から測る (DroneState と同じ)
            public Vector3 Launch { get; }

            public int LastSequence { get; set; } = -1;

            public Vector2 Move { get; set; }

            public float Climb { get; set; }

            public float InputAt { get; set; }

            public bool HasInput => Move != Vector2.Zero || Climb != 0f;

            public void Stop()
            {
                Move = Vector2.Zero;
                Climb = 0f;
            }
        }
    }
}

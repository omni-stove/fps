using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    // ビームと火炎は押している間の向き・弾の払い方・送った線を同じ規則で持つので、1 か所にまとめる。
    internal class HeldFire
    {
        private Vector3 _offset;
        private Vector3 _direction;
        private float _yaw;
        private float _lagTicks;

        public HeldFire(int shooterId, BeamState state)
        {
            ShooterId = shooterId;
            State = state;
        }

        public int ShooterId { get; }

        public BeamState State { get; }

        public ServerShot Shot { get; private set; }

        public bool Prepaid { get; set; }

        public int Ticks { get; set; }

        public Vector3 LastStart { get; set; }

        public Vector3 LastEnd { get; set; }

        // 押したときの始点と向きをそのまま使わないのは、押している間に動いたり振り向いたりしても照射や噴射を体に付いてこさせるため
        public void Aim(in ServerShot shot, PlannedRound planned, ServerBody body, float lastRecordedTick)
        {
            Shot = shot;
            _offset = planned.Origin - body.EyePosition;
            _direction = Vector3.Normalize(planned.Direction);
            _yaw = body.Yaw;
            _lagTicks = float.IsNaN(lastRecordedTick) ? 0f : Math.Max(0f, lastRecordedTick - shot.Tick);
        }

        public void Pose(ServerBody body, out Vector3 origin, out Vector3 direction)
        {
            var turn = body.Yaw - _yaw;
            origin = body.EyePosition + RotateYaw(_offset, turn);
            direction = RotateYaw(_direction, turn);
        }

        public float ViewTick(float lastRecordedTick)
        {
            return float.IsNaN(lastRecordedTick) ? Shot.Tick : Math.Max(0f, lastRecordedTick - _lagTicks);
        }

        // Unity の左手系で Y 軸まわりに回す (PlayerMotor.ToWorld と同じ向き)
        private static Vector3 RotateYaw(Vector3 value, float radians)
        {
            if (radians == 0f)
            {
                return value;
            }

            var sin = MathF.Sin(radians);
            var cos = MathF.Cos(radians);
            return new Vector3((value.X * cos) + (value.Z * sin), value.Y, (-value.X * sin) + (value.Z * cos));
        }
    }
}

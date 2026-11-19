using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct AircraftFlightCommand
    {
        public AircraftFlightCommand(Vector3 localAcceleration, Vector3 localAngularAcceleration, float liftRatio, float control)
        {
            LocalAcceleration = localAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            LiftRatio = liftRatio;
            Control = control;
        }

        public Vector3 LocalAcceleration { get; }

        public Vector3 LocalAngularAcceleration { get; }

        public float LiftRatio { get; }

        public float Control { get; }
    }

    // 機体から見た軸 (x 右・y 上・z 前) と回る向きを Unity の左手系に合わせるのは、Runtime が AddRelativeForce・AddRelativeTorque にそのまま渡せるようにするため。
    public static class AircraftFlight
    {
        public const int ThrottleAxis = 0;

        public const int PitchAxis = 1;

        public const int RollAxis = 2;

        public const int YawAxis = 3;

        private const float _degreesToRadians  = (float)(Math.PI / 180.0);

        // 揚力を重さまでで止めるのは、迎え角を持たない作りで速いほど浮くと、操縦しなくても勝手に昇ってしまうため。
        public static float LiftRatio(AircraftSpec spec, float forwardSpeed)
        {
            RequireSpec(spec);
            var ratio = Math.Max(0f, forwardSpeed) / spec.LiftSpeed;
            return Math.Min(1f, ratio * ratio);
        }

        public static float Control(AircraftSpec spec, float forwardSpeed)
        {
            RequireSpec(spec);
            var ratio = Math.Min(1f, Math.Max(0f, forwardSpeed) / spec.ControlSpeed);
            return Math.Max(spec.MinControl, ratio);
        }

        // 降下では重さも前へ押すので、最高速度より速くなる。サーバーの速すぎ判定にはこちらを使う。
        public static float TopSpeed(AircraftSpec spec, float gravity)
        {
            RequireSpec(spec);
            RequireGravity(gravity);
            return (float)Math.Sqrt((spec.MaxThrust + gravity) / spec.DragCoefficient);
        }

        public static AircraftFlightCommand Compute(
            AircraftSpec spec, VehicleMotorInput input, Vector3 localVelocity, Vector3 localAngularVelocity, float gravity)
        {
            RequireSpec(spec);
            RequireGravity(gravity);
            RequireFinite(localVelocity, nameof(localVelocity));
            RequireFinite(localAngularVelocity, nameof(localAngularVelocity));

            var forwardSpeed = Math.Max(0f, localVelocity.Z);
            var lift = LiftRatio(spec, forwardSpeed);
            var control = Control(spec, forwardSpeed);

            var throttle = input[ThrottleAxis];
            var thrust = Math.Max(0f, throttle) * spec.MaxThrust;
            var drag = spec.DragCoefficient * (1f + (Math.Max(0f, -throttle) * spec.AirBrakeDrag));
            var acceleration = new Vector3(0f, gravity * lift, thrust) - (localVelocity * (localVelocity.Length() * drag));

            // 横と上下へ流れる速さを機首の向きへ寄せるのは、翼が空気を受ける代わりで、これが無いと機首を回しても進む向きが曲がらないため。
            acceleration -= new Vector3(localVelocity.X, localVelocity.Y, 0f) * (spec.Alignment * control);

            var target = new Vector3(
                -input[PitchAxis] * spec.PitchRate,
                input[YawAxis] * spec.YawRate,
                -input[RollAxis] * spec.RollRate) * (_degreesToRadians * control);
            var angular = (target - localAngularVelocity) * spec.Responsiveness;
            return new AircraftFlightCommand(acceleration, angular, lift, control);
        }

        private static void RequireSpec(AircraftSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }
        }

        private static void RequireGravity(float gravity)
        {
            if (!(gravity >= 0f) || float.IsInfinity(gravity))
            {
                throw new ArgumentOutOfRangeException(nameof(gravity), gravity, "gravity must be a non-negative finite number");
            }
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!float.IsFinite(value.X) || !float.IsFinite(value.Y) || !float.IsFinite(value.Z))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}

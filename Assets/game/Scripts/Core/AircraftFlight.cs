using System;
using System.Numerics;

namespace Shooter.Core
{
    public readonly struct AircraftFlightCommand
    {
        public AircraftFlightCommand(
            Vector3 localAcceleration, Vector3 localAngularAcceleration, float liftRatio, float control, AircraftFlightState state)
        {
            LocalAcceleration = localAcceleration;
            LocalAngularAcceleration = localAngularAcceleration;
            LiftRatio = liftRatio;
            Control = control;
            State = state;
        }

        public Vector3 LocalAcceleration { get; }

        public Vector3 LocalAngularAcceleration { get; }

        public float LiftRatio { get; }

        public float Control { get; }

        public AircraftFlightState State { get; }
    }

    // 機体から見た軸 (x 右・y 上・z 前) と回る向きを Unity の左手系に合わせるのは、Runtime が AddRelativeForce・AddRelativeTorque にそのまま渡せるようにするため。
    public static class AircraftFlight
    {
        public const int ThrottleAxis = 0;

        public const int PitchAxis = 1;

        public const int RollAxis = 2;

        public const int YawAxis = 3;

        public static AircraftFlightCommand Compute(
            AircraftSpec spec,
            VehicleMotorInput input,
            Vector3 localVelocity,
            Vector3 localAngularVelocity,
            float gravity,
            AircraftGroundContact ground = default,
            AircraftCondition condition = default)
        {
            var aero = AircraftAero.Compute(spec, input, localVelocity, localAngularVelocity, gravity, condition);
            var thrust = Math.Max(0f, input[ThrottleAxis]) * spec.MaxThrust * condition.Thrust;

            var acceleration = aero.LocalAcceleration + new Vector3(0f, 0f, thrust) + ground.LocalAcceleration;
            var angular = aero.LocalAngularAcceleration + ground.LocalAngularAcceleration;
            var state = AircraftFlightState.From(localVelocity, acceleration, gravity, ground.Grounded);
            return new AircraftFlightCommand(acceleration, angular, aero.LiftRatio, aero.Control, state);
        }
    }
}

using System;

namespace Shooter.Core
{
    public readonly struct AircraftGroundCommand : IEquatable<AircraftGroundCommand>
    {
        public AircraftGroundCommand(float steerAngle, float brakeTorque)
        {
            SteerAngle = steerAngle;
            BrakeTorque = brakeTorque;
        }

        // 度で持ち、+ で右へ向けるのは、WheelCollider.steerAngle にそのまま渡せるようにするため。
        public float SteerAngle { get; }

        // 全部の車輪の合計でなく 1 つあたり (N·m) で持つのは、WheelCollider.brakeTorque にそのまま渡せ、車 (CarSpec) と同じ単位で比べられるようにするため。
        public float BrakeTorque { get; }

        public bool Equals(AircraftGroundCommand other)
        {
            return SteerAngle.Equals(other.SteerAngle) && BrakeTorque.Equals(other.BrakeTorque);
        }

        public override bool Equals(object obj)
        {
            return obj is AircraftGroundCommand other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(SteerAngle, BrakeTorque);
        }

        public static bool operator ==(AircraftGroundCommand left, AircraftGroundCommand right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AircraftGroundCommand left, AircraftGroundCommand right)
        {
            return !left.Equals(right);
        }
    }

    public static class AircraftGround
    {
        public static AircraftGroundCommand Compute(AircraftGroundSpec spec, VehicleMotorInput input, float forwardSpeed, bool grounded)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!float.IsFinite(forwardSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(forwardSpeed), forwardSpeed, "forwardSpeed must be finite");
            }

            // 浮いている間に前輪をまっすぐ戻しブレーキを放すのは、向いたままや止めたままの車輪で降りると、着いた瞬間に機体が振られるため。
            if (!grounded)
            {
                return default;
            }

            var steer = input[AircraftFlight.YawAxis] * SteerLimit(spec, forwardSpeed);
            return new AircraftGroundCommand(steer, BrakeTorque(spec, input));
        }

        // 速いほど絞るのは、滑走の速さで前輪を大きく向けると、機体が横へ振られて転ぶため。
        public static float SteerLimit(AircraftGroundSpec spec, float forwardSpeed)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            var t = Math.Clamp(Math.Abs(forwardSpeed) / spec.SteerFadeSpeed, 0f, 1f);
            return spec.MaxSteerAngle + ((spec.HighSpeedSteerAngle - spec.MaxSteerAngle) * t);
        }

        public static float BrakeTorque(AircraftGroundSpec spec, VehicleMotorInput input)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // 誰も操縦していない (入力が Idle) 間に止めておくのは、降りた後の機体が坂で転がり出さないようにするため。
            if (input.IsIdle)
            {
                return spec.IdleBrakeTorque;
            }

            return Math.Max(0f, -input[AircraftFlight.ThrottleAxis]) * spec.BrakeTorque;
        }
    }
}

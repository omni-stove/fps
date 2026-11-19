using System;

namespace Shooter.Core
{
    public readonly struct CarDriveCommand
    {
        public CarDriveCommand(float motorTorque, float brakeTorque, float steerAngle)
        {
            MotorTorque = motorTorque;
            BrakeTorque = brakeTorque;
            SteerAngle = steerAngle;
        }

        public float MotorTorque { get; }

        public float BrakeTorque { get; }

        public float SteerAngle { get; }
    }

    public static class CarDrive
    {
        // これより遅ければ、進む向きと逆のアクセルをブレーキでなく切り返しとして扱う。止まり切るまで待つと、ほぼ止まった車が動き出せないため。
        public const float DirectionChangeSpeed = 0.5f;

        public const int ThrottleAxis = 0;

        public const int SteerAxis = 1;

        public const int BrakeAxis = 2;

        public const int RightAxis = 3;

        public static CarDriveCommand Compute(CarSpec spec, VehicleMotorInput input, float forwardSpeed)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (float.IsNaN(forwardSpeed) || float.IsInfinity(forwardSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(forwardSpeed), forwardSpeed, "forwardSpeed must be finite");
            }

            var steer = input[SteerAxis] * spec.MaxSteerAngle;
            // 負のブレーキを捨てるのは、軸が -1 から 1 まで取れても、ブレーキで押し出す向きは無いため。
            var brake = Math.Max(0f, input[BrakeAxis]) * spec.BrakeTorque;
            var motor = 0f;
            var throttle = input[ThrottleAxis];
            if (throttle != 0f)
            {
                var opposing = (throttle > 0f && forwardSpeed < -DirectionChangeSpeed) || (throttle < 0f && forwardSpeed > DirectionChangeSpeed);
                if (opposing)
                {
                    brake = Math.Max(brake, Math.Abs(throttle) * spec.BrakeTorque);
                }
                else if (throttle > 0f ? forwardSpeed < spec.MaxForwardSpeed : -forwardSpeed < spec.MaxReverseSpeed)
                {
                    motor = throttle * spec.MotorTorque;
                }
            }
            else if (brake == 0f)
            {
                brake = spec.IdleBrakeTorque;
            }

            return new CarDriveCommand(motor, brake, steer);
        }

        public static bool IsTipped(CarSpec spec, float tiltDegrees)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (!(tiltDegrees >= 0f && tiltDegrees <= 180f))
            {
                throw new ArgumentOutOfRangeException(nameof(tiltDegrees), tiltDegrees, "tiltDegrees must be between 0 and 180");
            }

            return tiltDegrees > spec.FlipAngle;
        }

        public static bool CanRight(CarSpec spec, float tiltDegrees, float speed)
        {
            if (!(speed >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "speed must not be negative or NaN");
            }

            return IsTipped(spec, tiltDegrees) && speed <= spec.RightingMaxSpeed;
        }
    }
}

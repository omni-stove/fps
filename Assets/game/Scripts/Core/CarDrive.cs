using System;

namespace Fps.Core
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

            var steer = input.Steer * spec.MaxSteerAngle;
            var brake = input.Brake * spec.BrakeTorque;
            var motor = 0f;
            var throttle = input.Throttle;
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

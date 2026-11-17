using System;

namespace Shooter.Core
{
    public readonly struct VehicleMotorInput : IEquatable<VehicleMotorInput>
    {
        public VehicleMotorInput(float throttle, float steer, float brake, float vertical)
        {
            if (!(Math.Abs(throttle) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(throttle), throttle, "throttle must be between -1 and 1");
            }

            if (!(Math.Abs(steer) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(steer), steer, "steer must be between -1 and 1");
            }

            if (!(brake >= 0f && brake <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(brake), brake, "brake must be between 0 and 1");
            }

            if (!(Math.Abs(vertical) <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(vertical), vertical, "vertical must be between -1 and 1");
            }

            Throttle = throttle;
            Steer = steer;
            Brake = brake;
            Vertical = vertical;
        }

        public static VehicleMotorInput Idle => default;

        public float Throttle { get; }

        public float Steer { get; }

        public float Brake { get; }

        public float Vertical { get; }

        public bool IsIdle => Throttle == 0f && Steer == 0f && Brake == 0f && Vertical == 0f;

        public static bool operator ==(VehicleMotorInput left, VehicleMotorInput right) => left.Equals(right);

        public static bool operator !=(VehicleMotorInput left, VehicleMotorInput right) => !left.Equals(right);

        public bool Equals(VehicleMotorInput other)
        {
            return Throttle.Equals(other.Throttle) && Steer.Equals(other.Steer) && Brake.Equals(other.Brake) && Vertical.Equals(other.Vertical);
        }

        public override bool Equals(object obj) => obj is VehicleMotorInput other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = Throttle.GetHashCode();
                hash = (hash * 397) ^ Steer.GetHashCode();
                hash = (hash * 397) ^ Brake.GetHashCode();
                return (hash * 397) ^ Vertical.GetHashCode();
            }
        }

        public override string ToString()
        {
            return FormattableString.Invariant($"(throttle={Throttle:0.##} steer={Steer:0.##} brake={Brake:0.##} vertical={Vertical:0.##})");
        }
    }
}

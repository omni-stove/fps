using System;

namespace Shooter.Core
{
    // 文でなく値で持つのは、文にするのが UI だから。
    public readonly struct VehicleBoardHint : IEquatable<VehicleBoardHint>
    {
        public VehicleBoardHint(VehicleSeatRole role, string inputActionPath)
        {
            if (!Enum.IsDefined(typeof(VehicleSeatRole), role))
            {
                throw new ArgumentOutOfRangeException(nameof(role), role, "role is not a defined VehicleSeatRole");
            }

            Role = role;
            InputActionPath = inputActionPath ?? string.Empty;
        }

        public VehicleSeatRole Role { get; }

        public string InputActionPath { get; }

        public bool Equals(VehicleBoardHint other)
        {
            return Role == other.Role && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is VehicleBoardHint other && Equals(other);
        }

        public static bool operator ==(VehicleBoardHint left, VehicleBoardHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(VehicleBoardHint left, VehicleBoardHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Role, InputActionPath);
        }

        public override string ToString()
        {
            return $"vehicleBoard role={Role} input={InputActionPath}";
        }
    }
}

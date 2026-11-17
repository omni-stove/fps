using System;

namespace Shooter.Core
{
    // 文でなく値で持つのは、文にするのが UI だから。
    public readonly struct AmmoCrateHint : IEquatable<AmmoCrateHint>
    {
        public AmmoCrateHint(bool hasRoom, string inputActionPath)
        {
            HasRoom = hasRoom;
            InputActionPath = inputActionPath ?? string.Empty;
        }

        public bool HasRoom { get; }

        public string InputActionPath { get; }

        public bool Equals(AmmoCrateHint other)
        {
            return HasRoom == other.HasRoom && string.Equals(InputActionPath, other.InputActionPath, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is AmmoCrateHint other && Equals(other);
        }

        public static bool operator ==(AmmoCrateHint left, AmmoCrateHint right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AmmoCrateHint left, AmmoCrateHint right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(HasRoom, InputActionPath);
        }

        public override string ToString()
        {
            return $"ammoCrate room={HasRoom} input={InputActionPath}";
        }
    }
}

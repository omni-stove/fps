using System;

namespace Shooter.Runtime
{
    public readonly struct ActionDebugLine : IEquatable<ActionDebugLine>
    {
        private readonly string _text;

        public string Text => _text ?? string.Empty;

        public bool IsActive { get; }

        public ActionDebugLine(string text, bool isActive)
        {
            _text = text ?? string.Empty;
            IsActive = isActive;
        }

        public bool Equals(ActionDebugLine other)
        {
            return IsActive == other.IsActive && string.Equals(Text, other.Text, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is ActionDebugLine other && Equals(other);
        }

        public static bool operator ==(ActionDebugLine left, ActionDebugLine right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ActionDebugLine left, ActionDebugLine right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(StringComparer.Ordinal.GetHashCode(Text), IsActive);
        }

        public override string ToString()
        {
            return (IsActive ? "* " : "  ") + Text;
        }
    }
}

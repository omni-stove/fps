using System;
using Shooter.Core;

namespace Shooter.Runtime
{
    public readonly struct RebindSlot : IEquatable<RebindSlot>
    {
        public RebindSlot(string actionName, InputDeviceKind device, string part = null)
        {
            if (string.IsNullOrEmpty(actionName))
            {
                throw new ArgumentException("action name must not be empty", nameof(actionName));
            }

            if (device != InputDeviceKind.KeyboardMouse && device != InputDeviceKind.Gamepad)
            {
                throw new ArgumentOutOfRangeException(nameof(device), device, "unknown input device kind");
            }

            ActionName = actionName;
            Device = device;
            Part = string.IsNullOrEmpty(part) ? null : part;
        }

        public string ActionName { get; }

        public InputDeviceKind Device { get; }

        public string Part { get; }

        public bool Equals(RebindSlot other)
        {
            return string.Equals(ActionName, other.ActionName, StringComparison.Ordinal)
                && Device == other.Device
                && string.Equals(Part, other.Part, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return obj is RebindSlot other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = ActionName != null ? StringComparer.Ordinal.GetHashCode(ActionName) : 0;
                hash = (hash * 397) ^ (int)Device;
                return (hash * 397) ^ (Part != null ? StringComparer.OrdinalIgnoreCase.GetHashCode(Part) : 0);
            }
        }

        public override string ToString()
        {
            return Part == null ? $"{ActionName} ({Device})" : $"{ActionName}/{Part} ({Device})";
        }
    }
}

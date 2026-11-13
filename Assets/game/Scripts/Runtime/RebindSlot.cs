using System;
using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>
    /// 設定画面で割り当てを変えられる 1 か所。Player マップの <see cref="ActionName"/> の、<see cref="Device"/> の入力グループの最初の割り当て
    /// (<see cref="Part"/> があれば、その名前の合成の部品。例: Move の "up")。どの割り当てを指すかは <see cref="PlayerBindings.BindingIndexOf"/> が決める。
    /// </summary>
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

        /// <summary>Player マップのアクションの名前 (例: "Reload")。</summary>
        public string ActionName { get; }

        /// <summary>どの機器の割り当てか。</summary>
        public InputDeviceKind Device { get; }

        /// <summary>合成の部品の名前 (例: "up")。合成でなければ null。</summary>
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

using System;

namespace Shooter.Core
{
    public sealed class HoldToggleState
    {
        private HoldMode _mode;

        public HoldToggleState(HoldMode mode)
        {
            _mode = Validate(mode);
        }

        public HoldMode Mode
        {
            get => _mode;
            set
            {
                if (Validate(value) == _mode)
                {
                    return;
                }

                _mode = value;
                IsActive = false;
            }
        }

        public bool IsActive { get; private set; }

        public bool Update(bool isPressed, bool wasPressedThisFrame)
        {
            switch (_mode)
            {
                case HoldMode.Hold:
                    // isPressed だけで決めると、自動解除した直後に押しっぱなしのボタンでまた有効になってしまう。
                    IsActive = isPressed && (IsActive || wasPressedThisFrame);
                    break;
                case HoldMode.Toggle:
                    if (wasPressedThisFrame)
                    {
                        IsActive = !IsActive;
                    }

                    break;
            }

            return IsActive;
        }

        public void Release()
        {
            IsActive = false;
        }

        private static HoldMode Validate(HoldMode mode)
        {
            if (mode != HoldMode.Hold && mode != HoldMode.Toggle)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown hold mode");
            }

            return mode;
        }
    }
}

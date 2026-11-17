using System;

namespace Shooter.Core
{
    public sealed class ProneInput
    {
        public const string LogTag = "[Player]";

        public const string ReasonButton = "button";

        public const string ReasonDive = "dive";

        public const string ReasonCrouch = "crouch";

        public const string ReasonJump = "jump";

        public const string ReasonSprint = "sprint";

        public const string ReasonRule = "rule";

        public const string ReasonDeath = "death";

        public const string ReasonRespawn = "respawn";

        public const string ReasonOverride = "override";

        public const string ReasonSwim = "swim";

        private bool _wasSprinting;

        public event Action<ProneInput, string> Changed;

        public bool IsActive { get; private set; }

        public bool WasSprinting => _wasSprinting;

        // 予測の巻き戻しで戻すのは外から見た変化ではないので Changed は呼ばない。
        public void Restore(bool isActive, bool wasSprinting)
        {
            IsActive = isActive;
            _wasSprinting = wasSprinting;
        }

        public bool Update(bool canStart, bool pronePressed, bool crouchPressed, bool jumpPressed, bool sprinting, out bool dive)
        {
            var sprintStarted = sprinting && !_wasSprinting;
            _wasSprinting = sprinting;
            dive = false;

            if (IsActive)
            {
                var reason = pronePressed ? ReasonButton
                    : crouchPressed ? ReasonCrouch
                    : jumpPressed ? ReasonJump
                    : sprintStarted ? ReasonSprint
                    : null;
                if (reason != null)
                {
                    Set(false, reason);
                }

                return IsActive;
            }

            if (pronePressed && canStart)
            {
                dive = sprinting;
                Set(true, dive ? ReasonDive : ReasonButton);
            }

            return IsActive;
        }

        public bool Release(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("reason must not be empty", nameof(reason));
            }

            if (!IsActive)
            {
                return false;
            }

            Set(false, reason);
            return true;
        }

        public static string FormatLog(bool isActive, string reason)
        {
            return LogTag + " prone=" + (isActive ? "on" : "off") + " reason=" + reason;
        }

        private void Set(bool active, string reason)
        {
            IsActive = active;
            Changed?.Invoke(this, reason);
        }
    }
}

using System;

namespace Shooter.Core
{
    public sealed class CrouchInput
    {
        public const string LogTag = "[Player]";

        public const string ReasonButton = "button";

        public const string ReasonMode = "mode";

        public const string ReasonJump = "jump";

        public const string ReasonSprint = "sprint";

        public const string ReasonDeath = "death";

        public const string ReasonRespawn = "respawn";

        public const string ReasonDodge = "dodge";

        public const string ReasonProne = "prone";

        public const string ReasonSwim = "swim";

        private readonly HoldToggleState _state = new HoldToggleState(HoldMode.Hold);
        private readonly Func<bool> _canStand;
        private bool _hasMode;
        private bool _wasSprinting;

        public CrouchInput(Func<bool> canStand)
        {
            _canStand = canStand ?? throw new ArgumentNullException(nameof(canStand));
        }

        public event Action<CrouchInput, string> Changed;

        public HoldMode Mode => _state.Mode;

        public bool IsActive => _state.IsActive;

        public bool Update(HoldMode mode, bool isPressed, bool wasPressed, bool jumpPressed, bool sprinting)
        {
            SetMode(mode);

            var sprintStarted = sprinting && !_wasSprinting;
            _wasSprinting = sprinting;

            var wasActive = _state.IsActive;
            _state.Update(isPressed, wasPressed);
            if (_state.IsActive != wasActive)
            {
                Raise(ReasonButton);
            }

            // 同じ回に押した切り替えを打ち消さないよう、前からしゃがんでいたときだけ解除する
            // (立っていてしゃがみとジャンプを同時に押せばしゃがみジャンプになる)。
            if (wasActive && _state.IsActive && (jumpPressed || sprintStarted) && _canStand())
            {
                _state.Release();
                Raise(jumpPressed ? ReasonJump : ReasonSprint);
            }

            return _state.IsActive;
        }

        public bool Release(string reason)
        {
            if (string.IsNullOrEmpty(reason))
            {
                throw new ArgumentException("reason must not be empty", nameof(reason));
            }

            if (!_state.IsActive)
            {
                return false;
            }

            _state.Release();
            Raise(reason);
            return true;
        }

        public static string FormatLog(bool isActive, HoldMode mode, string reason)
        {
            return LogTag + " crouch=" + (isActive ? "on" : "off")
                + " mode=" + (mode == HoldMode.Toggle ? "toggle" : "hold")
                + " reason=" + reason;
        }

        private void SetMode(HoldMode mode)
        {
            if (_hasMode && mode == _state.Mode)
            {
                return;
            }

            // HoldToggleState が不正なモードを弾くので、ここでは確かめない。
            _state.Mode = mode;
            _hasMode = true;
            Raise(ReasonMode);
        }

        private void Raise(string reason)
        {
            Changed?.Invoke(this, reason);
        }
    }
}

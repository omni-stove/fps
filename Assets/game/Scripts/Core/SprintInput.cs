using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class SprintInput
    {
        public const string LogTag = "[Player]";

        public const float MoveThreshold = 0.1f;

        public const string ReasonButton = "button";

        public const string ReasonStopped = "stopped";

        public const string ReasonMode = "mode";

        public const string ReasonFire = "fire";

        public const string ReasonDeath = "death";

        public const string ReasonRespawn = "respawn";

        public const string ReasonAim = "aim";

        public const string ReasonDowned = "downed";

        public const string ReasonDodge = "dodge";

        public const string ReasonProne = "prone";

        public const string ReasonSwim = "swim";

        public const string ReasonStamina = "stamina";

        private readonly HoldToggleState _state = new HoldToggleState(HoldMode.Hold);
        private bool _hasMode;
        private bool _wasMoving;

        public event Action<SprintChange> Changed;

        public HoldMode Mode => _state.Mode;

        public bool IsActive => _state.IsActive;

        // Toggle で移動入力がなくなったら解除してからボタンを見るのは、止まりながら押した切り替えを次に動き出したときのために残すため。
        public bool Update(HoldMode mode, Vector2 move, bool isPressed, bool wasPressed)
        {
            SetMode(mode);

            // NaN は比較が偽になるので止まっている扱いになる。
            var moving = move.LengthSquared() >= MoveThreshold * MoveThreshold;
            var stopped = _wasMoving && !moving;
            _wasMoving = moving;

            // Hold は押し続けたまま止まって動き出したときにまたダッシュするよう、止まっても解除しない。
            if (stopped && _state.Mode == HoldMode.Toggle)
            {
                Release(ReasonStopped);
            }

            var wasActive = _state.IsActive;
            _state.Update(isPressed, wasPressed);
            if (_state.IsActive != wasActive)
            {
                Raise(ReasonButton);
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

        public static string FormatLog(SprintChange change)
        {
            return LogTag + " sprint=" + (change.IsActive ? "on" : "off")
                + " mode=" + (change.Mode == HoldMode.Toggle ? "toggle" : "hold")
                + " reason=" + change.Reason;
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
            Changed?.Invoke(new SprintChange(_state.IsActive, _state.Mode, reason));
        }
    }
}

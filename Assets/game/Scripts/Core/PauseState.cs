using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class PauseState
    {
        public const float FrozenTimeScale = 0f;

        private static readonly IReadOnlyList<string> _noActions = Array.Empty<string>();

        private readonly HashSet<string> _keepEnabled;

        // Close で戻すのを自分が止めたものだけにするのは、開く前から無効だったアクションを有効にしないため。
        private readonly List<string> _suspended = new List<string>();
        private readonly HashSet<string> _suspendedSet = new HashSet<string>(StringComparer.Ordinal);
        private PauseTimeMode _timeMode;
        private float _timeScaleToRestore = 1f;

        public PauseState(PauseTimeMode timeMode, IEnumerable<string> keepEnabledActions)
        {
            if (keepEnabledActions == null)
            {
                throw new ArgumentNullException(nameof(keepEnabledActions));
            }

            _timeMode = Validate(timeMode);
            _keepEnabled = new HashSet<string>(StringComparer.Ordinal);
            foreach (var action in keepEnabledActions)
            {
                _keepEnabled.Add(ValidateActionName(action, nameof(keepEnabledActions)));
            }
        }

        // 閉じている間だけ変えられるのは、開いている途中で変えると、止めた時間を戻せなくなるため。
        public PauseTimeMode TimeMode
        {
            get => _timeMode;
            set
            {
                if (Validate(value) == _timeMode)
                {
                    return;
                }

                if (IsOpen)
                {
                    throw new InvalidOperationException("the pause time mode cannot change while the pause menu is open");
                }

                _timeMode = value;
            }
        }

        public bool IsOpen { get; private set; }

        public bool FreezesTime => _timeMode == PauseTimeMode.Freeze;

        public IReadOnlyList<string> SuspendedActions => _suspended;

        public bool KeepsEnabled(string action)
        {
            return action != null && _keepEnabled.Contains(action);
        }

        public bool Open(float currentTimeScale, out float? timeScale)
        {
            if (!(currentTimeScale >= 0f) || float.IsInfinity(currentTimeScale))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentTimeScale), currentTimeScale, "time scale must be a non-negative finite number");
            }

            timeScale = null;
            if (IsOpen)
            {
                return false;
            }

            IsOpen = true;
            if (FreezesTime)
            {
                _timeScaleToRestore = currentTimeScale;
                timeScale = FrozenTimeScale;
            }

            return true;
        }

        // 開いている間ずっと何度でも呼べ、まだ止めていないものだけを返すのは、開いている途中で有効にされたアクション
        // (出現し直したプレイヤーの部品など) も止められるようにするため。
        public IReadOnlyList<string> Suspend(IEnumerable<string> enabledActions)
        {
            if (enabledActions == null)
            {
                throw new ArgumentNullException(nameof(enabledActions));
            }

            if (!IsOpen)
            {
                throw new InvalidOperationException("input can only be suspended while the pause menu is open");
            }

            // 途中の名前が不正で投げたときに、それより前の名前だけ止めたことにならないよう、先にすべて確かめる。
            var candidates = new List<string>();
            foreach (var action in enabledActions)
            {
                candidates.Add(ValidateActionName(action, nameof(enabledActions)));
            }

            List<string> added = null;
            foreach (var action in candidates)
            {
                if (_keepEnabled.Contains(action) || !_suspendedSet.Add(action))
                {
                    continue;
                }

                _suspended.Add(action);
                (added ??= new List<string>()).Add(action);
            }

            return added ?? _noActions;
        }

        public bool Close(out float? timeScale, out IReadOnlyList<string> actionsToEnable)
        {
            timeScale = null;
            actionsToEnable = _noActions;
            if (!IsOpen)
            {
                return false;
            }

            IsOpen = false;
            if (FreezesTime)
            {
                timeScale = _timeScaleToRestore;
            }

            if (_suspended.Count > 0)
            {
                actionsToEnable = _suspended.ToArray();
                _suspended.Clear();
                _suspendedSet.Clear();
            }

            return true;
        }

        private static PauseTimeMode Validate(PauseTimeMode mode)
        {
            if (mode != PauseTimeMode.KeepRunning && mode != PauseTimeMode.Freeze)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown pause time mode");
            }

            return mode;
        }

        private static string ValidateActionName(string action, string paramName)
        {
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentException("action names must not be null or empty", paramName);
            }

            return action;
        }
    }
}

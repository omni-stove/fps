using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// ポーズ画面が開いているかと、開いている間の時間と入力の止め方。
    /// <para>
    /// 時間: <see cref="PauseTimeMode.Freeze"/> なら <see cref="Open"/> で開く前の速さを覚えて <see cref="FrozenTimeScale"/> を返し、
    /// <see cref="Close"/> で覚えた速さを返す。<see cref="PauseTimeMode.KeepRunning"/> ではどちらも null を返し、時間に触らない。
    /// </para>
    /// <para>
    /// 入力: 開いている間は、有効なアクションのうち止めないもの (<see cref="KeepsEnabled"/>、ポーズの開閉に使うアクション) 以外を止める。
    /// 開いている途中で有効にされたアクション (出現し直したプレイヤーの部品など) も止められるよう、<see cref="Suspend"/> は
    /// 開いている間ずっと何度でも呼べ、まだ止めていないものだけを返す。<see cref="Close"/> は自分が止めたものだけを返す
    /// (開く前から無効だったものを有効にしない)。
    /// </para>
    /// アクションの名前の比べ方は大文字と小文字を区別する。
    /// </summary>
    public sealed class PauseState
    {
        /// <summary><see cref="PauseTimeMode.Freeze"/> で開いている間の時間の速さ。</summary>
        public const float FrozenTimeScale = 0f;

        private static readonly IReadOnlyList<string> _noActions = Array.Empty<string>();

        private readonly HashSet<string> _keepEnabled;
        private readonly List<string> _suspended = new List<string>();
        private readonly HashSet<string> _suspendedSet = new HashSet<string>(StringComparer.Ordinal);
        private PauseTimeMode _timeMode;
        private float _timeScaleToRestore = 1f;

        /// <param name="timeMode">開いている間に時間を止めるか。</param>
        /// <param name="keepEnabledActions">開いている間も止めないアクション (ポーズの開閉に使うものなど)。</param>
        /// <exception cref="ArgumentNullException">keepEnabledActions が null。</exception>
        /// <exception cref="ArgumentException">keepEnabledActions に null か空の名前がある。</exception>
        /// <exception cref="ArgumentOutOfRangeException">timeMode が知らない値。</exception>
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

        /// <summary>
        /// 開いている間に時間を止めるか。閉じている間だけ変えられる (開いている途中で変えると、止めた時間を戻せなくなるため)。
        /// </summary>
        /// <exception cref="InvalidOperationException">開いている間に違う値を入れた。</exception>
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

        /// <summary>開いているか。</summary>
        public bool IsOpen { get; private set; }

        /// <summary>開いている間に時間を止めるか。</summary>
        public bool FreezesTime => _timeMode == PauseTimeMode.Freeze;

        /// <summary>今止めているアクション (止めた順)。閉じている間は空。</summary>
        public IReadOnlyList<string> SuspendedActions => _suspended;

        /// <summary>action が開いている間も止めないアクションか。</summary>
        public bool KeepsEnabled(string action)
        {
            return action != null && _keepEnabled.Contains(action);
        }

        /// <summary>
        /// 開く。既に開いていれば何もせず false。
        /// </summary>
        /// <param name="currentTimeScale">今の時間の速さ。<see cref="PauseTimeMode.Freeze"/> なら閉じたときにこの速さへ戻す。</param>
        /// <param name="timeScale">入れる時間の速さ。時間に触らないとき (止めないモードか、既に開いている) は null。</param>
        /// <exception cref="ArgumentOutOfRangeException">currentTimeScale が負か有限でない。</exception>
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

        /// <summary>
        /// 開いている間に止めるアクションを決める。enabledActions (今有効なもの) のうち、止めないものと既に止めたものを除いて返し、
        /// 閉じたときに戻すものとして覚える。開いた直後と、開いている間のフレームごとに呼ぶ。
        /// </summary>
        /// <exception cref="ArgumentNullException">enabledActions が null。</exception>
        /// <exception cref="ArgumentException">enabledActions に null か空の名前がある。</exception>
        /// <exception cref="InvalidOperationException">閉じている。</exception>
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

        /// <summary>
        /// 閉じる。開いていなければ何もせず false。
        /// </summary>
        /// <param name="timeScale">戻す時間の速さ (開いたときの速さ)。時間に触らないときは null。</param>
        /// <param name="actionsToEnable">止めたので有効に戻すアクション (止めた順)。</param>
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

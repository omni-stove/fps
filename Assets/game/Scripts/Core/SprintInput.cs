using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// ダッシュボタンと移動入力から、ダッシュ中かどうかを決める。中で <see cref="HoldToggleState"/> を持ち、
    /// 押している間 (<see cref="HoldMode.Hold"/>) か押すたびに切り替え (<see cref="HoldMode.Toggle"/>) かは <see cref="Update"/> のたびに渡す。
    /// Toggle では移動入力がなくなったら解除する。射撃・死亡など外の理由では <see cref="Release"/> を呼ぶ。
    /// 有効・無効やモードが変わるたびに <see cref="Changed"/> を呼ぶ。
    /// </summary>
    public sealed class SprintInput
    {
        public const string LogTag = "[Player]";

        /// <summary>移動入力の大きさがこれより小さければ止まっているとみなす。</summary>
        public const float MoveThreshold = 0.1f;

        /// <summary>ボタンで有効・無効が変わった。</summary>
        public const string ReasonButton = "button";

        /// <summary>移動入力がなくなった (Toggle のときだけ解除する)。</summary>
        public const string ReasonStopped = "stopped";

        /// <summary>モードが変わった (初めての <see cref="Update"/> を含む)。モードが変わると解除する。</summary>
        public const string ReasonMode = "mode";

        /// <summary>射撃した。</summary>
        public const string ReasonFire = "fire";

        /// <summary>死亡した。</summary>
        public const string ReasonDeath = "death";

        /// <summary>リスポーンした。</summary>
        public const string ReasonRespawn = "respawn";

        private readonly HoldToggleState _state = new HoldToggleState(HoldMode.Hold);
        private bool _hasMode;
        private bool _wasMoving;

        /// <summary>有効・無効かモードが変わったときに呼ばれる。</summary>
        public event Action<SprintChange> Changed;

        /// <summary>今のモード。初めての <see cref="Update"/> の前は <see cref="HoldMode.Hold"/>。</summary>
        public HoldMode Mode => _state.Mode;

        /// <summary>ダッシュ中か。</summary>
        public bool IsActive => _state.IsActive;

        /// <summary>
        /// 1 回分 (オフラインは 1 フレーム、ネット対戦は 1 tick) の入力を渡し、ダッシュ中かを返す。
        /// mode が今と違えば解除してから入れ替える。Toggle で移動入力がなくなったら解除し、その後でボタンを見る
        /// (止まりながら押した切り替えは、次に動き出したときのために残す)。
        /// </summary>
        /// <param name="mode">今使っている入力機器でのダッシュのモード。</param>
        /// <param name="move">移動入力。</param>
        /// <param name="isPressed">ダッシュボタンが押されているか。</param>
        /// <param name="wasPressed">前回の Update からダッシュボタンが押されたか。</param>
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

        /// <summary>
        /// 外の理由で解除する (射撃・死亡・ADS など)。Hold でもボタンを押し直すまでダッシュしない。
        /// ダッシュ中だったら <see cref="Changed"/> を呼んで true、既に解除されていれば何もせず false。
        /// </summary>
        /// <param name="reason">ログに出す理由。<see cref="ReasonFire"/> など。</param>
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

        /// <summary>変化のログ。例: <c>[Player] sprint=off mode=toggle reason=stopped</c>。</summary>
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

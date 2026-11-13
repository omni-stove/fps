using System;

namespace Fps.Core
{
    /// <summary>
    /// しゃがみボタンから「しゃがみたいか」を決める。中で <see cref="HoldToggleState"/> を持ち、
    /// 押している間 (<see cref="HoldMode.Hold"/>) か押すたびに切り替え (<see cref="HoldMode.Toggle"/>) かは <see cref="Update"/> のたびに渡す。
    /// しゃがんでいる間にジャンプするかダッシュを始めると解除する。ただし頭上が塞がっていて立てないときは解除しない。
    /// 死亡・リスポーンなど外の理由では <see cref="Release"/> を呼ぶ。
    /// 返す値は姿勢の希望で、実際に立つかどうか (頭上の検査) は姿勢を当てる側が決める。
    /// 有効・無効やモードが変わるたびに <see cref="Changed"/> を呼ぶ。
    /// </summary>
    public sealed class CrouchInput
    {
        public const string LogTag = "[Player]";

        /// <summary>ボタンで有効・無効が変わった。</summary>
        public const string ReasonButton = "button";

        /// <summary>モードが変わった (初めての <see cref="Update"/> を含む)。モードが変わると解除する。</summary>
        public const string ReasonMode = "mode";

        /// <summary>ジャンプした。</summary>
        public const string ReasonJump = "jump";

        /// <summary>ダッシュを始めた。</summary>
        public const string ReasonSprint = "sprint";

        /// <summary>死亡した。</summary>
        public const string ReasonDeath = "death";

        /// <summary>リスポーンした。</summary>
        public const string ReasonRespawn = "respawn";

        private readonly HoldToggleState _state = new HoldToggleState(HoldMode.Hold);
        private readonly Func<bool> _canStand;
        private bool _hasMode;
        private bool _wasSprinting;

        /// <param name="canStand">
        /// 今立てるか (頭上が空いているか)。ジャンプやダッシュで解除しようとしたときだけ呼ぶ。
        /// </param>
        public CrouchInput(Func<bool> canStand)
        {
            _canStand = canStand ?? throw new ArgumentNullException(nameof(canStand));
        }

        /// <summary>有効・無効かモードが変わったときに、変化した後の状態と理由 (<see cref="ReasonButton"/> など) を渡して呼ばれる。</summary>
        public event Action<CrouchInput, string> Changed;

        /// <summary>今のモード。初めての <see cref="Update"/> の前は <see cref="HoldMode.Hold"/>。</summary>
        public HoldMode Mode => _state.Mode;

        /// <summary>しゃがみたいか。</summary>
        public bool IsActive => _state.IsActive;

        /// <summary>
        /// 1 回分 (オフラインは 1 フレーム、ネット対戦は 1 tick) の入力を渡し、しゃがみたいかを返す。
        /// mode が今と違えば解除してから入れ替え、ボタンを見て、その前からしゃがんでいてジャンプかダッシュの開始があれば解除する。
        /// </summary>
        /// <param name="mode">今使っている入力機器でのしゃがみのモード。</param>
        /// <param name="isPressed">しゃがみボタンが押されているか。</param>
        /// <param name="wasPressed">前回の Update からしゃがみボタンが押されたか。</param>
        /// <param name="jumpPressed">前回の Update からジャンプが押されたか。</param>
        /// <param name="sprinting">ダッシュ中か。前回 false で今回 true のときだけダッシュの開始とみなす。</param>
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

        /// <summary>
        /// 外の理由で解除する (死亡・リスポーンなど)。頭上は確かめない。Hold でもボタンを押し直すまでしゃがまない。
        /// しゃがみたい状態だったら <see cref="Changed"/> を呼んで true、既に解除されていれば何もせず false。
        /// </summary>
        /// <param name="reason">ログに出す理由。<see cref="ReasonDeath"/> など。</param>
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

        /// <summary>変化のログ。例: <c>[Player] crouch=off mode=toggle reason=jump</c>。</summary>
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

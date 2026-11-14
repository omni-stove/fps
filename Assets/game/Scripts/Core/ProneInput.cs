using System;

namespace Fps.Core
{
    /// <summary>
    /// 伏せボタン (Prone) から「伏せたいか」を決める。押すたびに切り替え、ダッシュ中に押して伏せたら飛び込み (<see cref="ProneDive"/>) を始めさせる。
    /// 伏せている間にしゃがみ・ジャンプを押すかダッシュを始めると解除する。死亡やルールの切り替えなど外の理由では <see cref="Release"/> を呼ぶ。
    /// 返す値は姿勢の希望で、実際に起き上がれるか (頭上の検査) は姿勢を当てる側が決める。
    /// 有効・無効が変わるたびに <see cref="Changed"/> を呼ぶ。
    /// </summary>
    public sealed class ProneInput
    {
        public const string LogTag = "[Player]";

        /// <summary>伏せボタンで切り替えた。</summary>
        public const string ReasonButton = "button";

        /// <summary>ダッシュ中に伏せボタンを押して、飛び込んで伏せた。</summary>
        public const string ReasonDive = "dive";

        /// <summary>しゃがみボタンを押した (しゃがみへ起き上がる)。</summary>
        public const string ReasonCrouch = "crouch";

        /// <summary>ジャンプを押した。</summary>
        public const string ReasonJump = "jump";

        /// <summary>ダッシュを始めた。</summary>
        public const string ReasonSprint = "sprint";

        /// <summary>ルールで伏せが使えなくなった。</summary>
        public const string ReasonRule = "rule";

        /// <summary>死亡した。</summary>
        public const string ReasonDeath = "death";

        /// <summary>リスポーンした。</summary>
        public const string ReasonRespawn = "respawn";

        /// <summary>はしごなど移動の横取りが始まった。</summary>
        public const string ReasonOverride = "override";

        private bool _wasSprinting;

        /// <summary>有効・無効が変わったときに、変化した後の状態と理由 (<see cref="ReasonButton"/> など) を渡して呼ばれる。</summary>
        public event Action<ProneInput, string> Changed;

        /// <summary>伏せたいか。</summary>
        public bool IsActive { get; private set; }

        /// <summary>前回の <see cref="Update"/> でダッシュ中だったか。</summary>
        public bool WasSprinting => _wasSprinting;

        /// <summary>
        /// 予測の巻き戻しとサーバーの tick の間で、写した中身 (<see cref="IsActive"/>・<see cref="WasSprinting"/>) に戻す。
        /// 外から見た変化ではないので <see cref="Changed"/> は呼ばない。
        /// </summary>
        public void Restore(bool isActive, bool wasSprinting)
        {
            IsActive = isActive;
            _wasSprinting = wasSprinting;
        }

        /// <summary>
        /// 1 回分 (1 フレーム) の入力を渡し、伏せたいかを返す。
        /// 伏せていなければ、canStart のときに限り伏せボタンで伏せる。そのときダッシュ中なら dive を true にする。
        /// 伏せていれば、伏せボタン・しゃがみボタン・ジャンプ・ダッシュの開始のどれかで解除する (この順に理由を決める)。
        /// </summary>
        /// <param name="canStart">今伏せ始めてよいか (ルールが On で、接地していて、回避などの最中でない)。解除には関わらない。</param>
        /// <param name="pronePressed">前回の Update から伏せボタンが押されたか。</param>
        /// <param name="crouchPressed">前回の Update からしゃがみボタンが押されたか。</param>
        /// <param name="jumpPressed">前回の Update からジャンプが押されたか。</param>
        /// <param name="sprinting">ダッシュ中か。前回 false で今回 true のときだけダッシュの開始とみなす。</param>
        /// <param name="dive">この回に、ダッシュ中の伏せボタンで伏せ始めたか。</param>
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

        /// <summary>
        /// 外の理由で解除する (死亡・ルールの切り替えなど)。頭上は確かめない。
        /// 伏せたい状態だったら <see cref="Changed"/> を呼んで true、既に解除されていれば何もせず false。
        /// </summary>
        /// <param name="reason">ログに出す理由。<see cref="ReasonDeath"/> など。</param>
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

        /// <summary>変化のログ。例: <c>[Player] prone=on reason=dive</c>。</summary>
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

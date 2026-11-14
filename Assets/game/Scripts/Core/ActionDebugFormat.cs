using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// プレイヤーの今のアクション (<see cref="PlayerActionState"/>) を、デバッグ表示の行に組み立てる。
    /// 並びはダッシュ・しゃがみ・回避・乗り越え・接地・カバー・ブラインドファイア・ADS・射撃・リロード・ダウン・肩・視点・武器・ばらつき。
    /// 状態の有無を表す行は <c>名前: ON</c> / <c>名前: OFF</c> で、ON のときだけ <see cref="ActionDebugLine.IsActive"/> が true。
    /// 肩・視点・武器・ばらつきは値を出すだけで、有効にはならない。項目名と値の語は <see cref="GameText"/> から今の言語で引く。
    /// </summary>
    public static class ActionDebugFormat
    {
        /// <summary><see cref="Lines"/> が返す行の数。</summary>
        public const int LineCount = 15;

        public const string On = "ON";

        public const string Off = "OFF";

        /// <summary>武器を持っていない・アクティブリロードを未判定のときの値。</summary>
        public const string None = "-";

        /// <summary>state の各項目を 1 行ずつ、<see cref="LineCount"/> 行にして返す。</summary>
        /// <example>
        /// ADS 中にリロードを 45% 進めたなら、ADS の行は <c>ADS: ON</c> (有効)、
        /// リロードの行は <c>リロード: ON 45% (アクティブ: -)</c> (有効)。
        /// </example>
        public static ActionDebugLine[] Lines(PlayerActionState state)
        {
            return new[]
            {
                Toggle("sprint", state.IsSprinting),
                Toggle("crouch", state.IsCrouching),
                Toggle("dodge", state.IsDodging),
                Toggle("vault", state.IsVaulting),
                Toggle("grounded", state.IsGrounded),
                Toggle("cover", state.IsInCover),
                Toggle("blindFire", state.IsBlindFiring),
                Toggle("ads", state.IsAiming),
                Toggle("fire", state.IsFiring),
                Value("reload", Reload(state.IsReloading, state.ReloadProgress, state.ActiveReload), state.IsReloading),
                Toggle("downed", state.IsDowned),
                Value("shoulder", Shoulder(state.Shoulder), false),
                Value("view", View(state.ViewMode), false),
                Value("weapon", state.WeaponName.Length > 0 ? state.WeaponName : None, false),
                Value("spread", Spread(state.SpreadDegrees), false),
            };
        }

        /// <summary>
        /// リロードの行の値。リロード中なら進みを切り捨てた百分率を付ける。直前のアクティブリロードの結果は、成功でリロードが
        /// その場で終わった後も残るので、リロード中かどうかに関わらず括弧で出す。
        /// </summary>
        /// <example><c>Reload(true, 0.456f, null)</c> は <c>ON 45% (アクティブ: -)</c>、<c>Reload(false, 0f, Success)</c> は <c>OFF (アクティブ: 成功)</c></example>
        /// <param name="reloading">リロード中か。</param>
        /// <param name="progress">0 以上 1 以下。リロード中でなければ使わない。</param>
        /// <param name="activeReload">直前のアクティブリロードの結果。まだ無ければ null。</param>
        public static string Reload(bool reloading, float progress, ActiveReloadResult? activeReload)
        {
            if (!(progress >= 0f && progress <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(progress), progress, "progress must be between 0 and 1");
            }

            var active = " (" + GameText.Format("actionDebug.activeReload", ActiveReload(activeReload)) + ")";
            if (!reloading)
            {
                return Off + active;
            }

            // 0.29f は 0.28999999... なので、そのまま切り捨てると 28% になる。表せない端数を丸めてから切り捨てる。
            var percent = (int)Math.Floor(Math.Round(progress * 100d, 4));
            return On + " " + percent.ToString(CultureInfo.InvariantCulture) + "%" + active;
        }

        /// <example><c>Spread(2.5f)</c> は <c>2.5°</c>、<c>Spread(0f)</c> は <c>0.0°</c> (小数 1 桁)</example>
        public static string Spread(float degrees)
        {
            if (!(degrees >= 0f) || float.IsInfinity(degrees))
            {
                throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "degrees must be a non-negative finite number");
            }

            return degrees.ToString("0.0", CultureInfo.InvariantCulture) + "°";
        }

        private static ActionDebugLine Toggle(string labelKey, bool active)
        {
            return Value(labelKey, active ? On : Off, active);
        }

        private static ActionDebugLine Value(string labelKey, string value, bool active)
        {
            return new ActionDebugLine(GameText.Get("actionDebug.label." + labelKey) + ": " + value, active);
        }

        private static string ActiveReload(ActiveReloadResult? result)
        {
            if (!result.HasValue)
            {
                return None;
            }

            switch (result.Value)
            {
                case ActiveReloadResult.Success:
                    return GameText.Get("actionDebug.activeReload.success");
                case ActiveReloadResult.Fail:
                    return GameText.Get("actionDebug.activeReload.fail");
                default:
                    throw new ArgumentOutOfRangeException(nameof(result), result, "result is not a defined ActiveReloadResult");
            }
        }

        private static string Shoulder(ShoulderSide shoulder)
        {
            switch (shoulder)
            {
                case ShoulderSide.Right:
                    return GameText.Get("actionDebug.shoulder.right");
                case ShoulderSide.Left:
                    return GameText.Get("actionDebug.shoulder.left");
                default:
                    throw new ArgumentOutOfRangeException(nameof(shoulder), shoulder, "shoulder is not a defined ShoulderSide");
            }
        }

        private static string View(ViewMode viewMode)
        {
            switch (viewMode)
            {
                case ViewMode.FirstPerson:
                    return GameText.Get("actionDebug.view.firstPerson");
                case ViewMode.ThirdPerson:
                    return GameText.Get("actionDebug.view.thirdPerson");
                default:
                    throw new ArgumentOutOfRangeException(nameof(viewMode), viewMode, "viewMode is not a defined ViewMode");
            }
        }
    }
}

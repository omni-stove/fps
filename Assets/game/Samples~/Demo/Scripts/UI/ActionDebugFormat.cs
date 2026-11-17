using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class ActionDebugFormat
    {
        public const int LineCount = 16;

        public const string On = "ON";

        public const string Off = "OFF";

        public const string None = "-";

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
                Toggle("inVehicle", state.IsInVehicle),
                Value("shoulder", Shoulder(state.Shoulder), false),
                Value("view", View(state.ViewMode), false),
                Value("weapon", state.WeaponName.Length > 0 ? state.WeaponName : None, false),
                Value("spread", Spread(state.SpreadDegrees), false),
            };
        }

        // 直前のアクティブリロードの結果は、成功でリロードがその場で終わった後も残るので、リロード中かどうかに関わらず括弧で出す。
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

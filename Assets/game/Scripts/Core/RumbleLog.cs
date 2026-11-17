using System.Globalization;

namespace Shooter.Core
{
    /// <summary>ゲームパッドの振動の <c>[Rumble]</c> 付きログの文言。</summary>
    public static class RumbleLog
    {
        public const string Tag = "[Rumble]";

        /// <summary>出来事を受けて振動を足した。source は出来事の種類 (fire・damage・explosion)、detail は添える説明 (空なら付けない)。</summary>
        /// <example><c>[Rumble] pulse source=explosion low=0.500 high=0.300 seconds=0.45 distance=12.5</c></example>
        public static string Pulse(string source, RumblePulse pulse, string detail)
        {
            var line = string.Format(
                CultureInfo.InvariantCulture,
                "{0} pulse source={1} low={2:0.000} high={3:0.000} seconds={4:0.00}",
                Tag,
                string.IsNullOrEmpty(source) ? "unknown" : source,
                pulse.Low,
                pulse.High,
                pulse.Seconds);
            return string.IsNullOrEmpty(detail) ? line : line + " " + detail;
        }

        /// <summary>動いていたモーターを止めた。reason は止めた訳 (expired・paused・settingsOff・keyboardMouse・disabled)。</summary>
        /// <example><c>[Rumble] stopped reason=paused</c></example>
        public static string Stopped(string reason)
        {
            return Tag + " stopped reason=" + (string.IsNullOrEmpty(reason) ? "unknown" : reason);
        }

        /// <summary>モーターを回す機器が別のゲームパッドに替わったので、前の機器を止めた。</summary>
        /// <example><c>[Rumble] device changed from=XInputControllerWindows to=DualSenseGamepadHID</c></example>
        public static string DeviceChanged(string from, string to)
        {
            return Tag + " device changed from=" + Token(from) + " to=" + Token(to);
        }

        /// <example><c>distance=12.5</c></example>
        public static string Distance(float distance)
        {
            return "distance=" + distance.ToString("0.0", CultureInfo.InvariantCulture);
        }

        /// <example><c>amount=25</c></example>
        public static string Amount(int amount)
        {
            return "amount=" + amount.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>weapon=rifle</c></example>
        public static string Weapon(string weaponId)
        {
            return "weapon=" + Token(weaponId);
        }

        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            return text.Trim().Replace(' ', '_');
        }
    }
}

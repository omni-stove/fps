using System.Globalization;
using UnityEngine;

namespace Shooter.Runtime
{
    public static class StreakRewardLog
    {
        public const string Tag = "[Streak]";

        public static string Uav(string user, int marked, float seconds)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} uav user={1} marked={2} seconds={3:0.00}",
                Tag,
                Token(user),
                marked,
                seconds);
        }

        public static string AirstrikeCalled(string user, Vector3 center, float headingDegrees, int strikes, float intervalSeconds, float lineLength)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} airstrike called user={1} at=({2:0.0}, {3:0.0}, {4:0.0}) heading={5:0.0} strikes={6} interval={7:0.00} line={8:0.0}",
                Tag,
                Token(user),
                center.x,
                center.y,
                center.z,
                headingDegrees,
                strikes,
                intervalSeconds,
                lineLength);
        }

        public static string AirstrikeStrike(string user, int strike, int strikes, GrenadeExplosion result)
        {
            var c = result.Center;
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} airstrike strike={1}/{2} user={3} at=({4:0.0}, {5:0.0}, {6:0.0}) damaged={7} killed={8}",
                Tag,
                strike,
                strikes,
                Token(user),
                c.x,
                c.y,
                c.z,
                result.Damaged,
                result.Killed);
        }

        // 名前に空白があってもログの区切りが崩れないよう、空白は _ に、空なら none にする。
        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "none";
            }

            var chars = text.Trim().ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (char.IsWhiteSpace(chars[i]))
                {
                    chars[i] = '_';
                }
            }

            return new string(chars);
        }
    }
}

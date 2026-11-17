using System.Globalization;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>ストリークの報酬 (<see cref="UavReward"/>・<see cref="AirstrikeReward"/>) の <c>[Streak]</c> 付きログの文言。</summary>
    public static class StreakRewardLog
    {
        public const string Tag = "[Streak]";

        /// <summary>UAV を使い、敵に印を付けた。</summary>
        /// <example><c>[Streak] uav user=Player marked=3 seconds=30.00</c></example>
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

        /// <summary>空爆を呼んだ。</summary>
        /// <example><c>[Streak] airstrike called user=Player at=(1.0, 0.0, 20.0) strikes=5 interval=0.40 spread=6.0</c></example>
        public static string AirstrikeCalled(string user, Vector3 center, int strikes, float intervalSeconds, float spreadRadius)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0} airstrike called user={1} at=({2:0.0}, {3:0.0}, {4:0.0}) strikes={5} interval={6:0.00} spread={7:0.0}",
                Tag,
                Token(user),
                center.x,
                center.y,
                center.z,
                strikes,
                intervalSeconds,
                spreadRadius);
        }

        /// <summary>空爆の 1 発が爆発した。strike は 1 から数える。</summary>
        /// <example><c>[Streak] airstrike strike=2/5 user=Player at=(2.3, 0.1, 18.7) damaged=1 killed=1</c></example>
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

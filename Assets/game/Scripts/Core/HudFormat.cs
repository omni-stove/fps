using System;
using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// HUD に出す文字列の書式。体力は数値だけ、弾数は「弾倉 / 予備」、残り時間は mm:ss。
    /// 値がまだ無いときは <see cref="NoHealth"/> などの仮の文字列を出す。
    /// </summary>
    public static class HudFormat
    {
        /// <summary>体力の源がまだ無いときの表示。</summary>
        public const string NoHealth = "--";

        /// <summary>武器がまだ無いときの表示。</summary>
        public const string NoAmmo = "-- / --";

        /// <summary>試合がまだ無いときの表示。</summary>
        public const string NoTime = "--:--";

        /// <summary>弾倉と予備の間に置く区切り。</summary>
        public const string AmmoSeparator = " / ";

        /// <summary>mm:ss で表せる最大の秒数 (99:59)。これより長い残り時間は 99:59 に切り詰める。</summary>
        public const int MaxDisplayedSeconds = 99 * 60 + 59;

        /// <example><c>Health(75)</c> は <c>75</c></example>
        public static string Health(int current)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            return current.ToString(CultureInfo.InvariantCulture);
        }

        /// <example><c>Ammo(30, 90)</c> は <c>30 / 90</c></example>
        public static string Ammo(int magazine, int reserve)
        {
            if (magazine < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(magazine), magazine, "magazine must not be negative");
            }

            if (reserve < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(reserve), reserve, "reserve must not be negative");
            }

            return magazine.ToString(CultureInfo.InvariantCulture) + AmmoSeparator + reserve.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// 残り時間として表示する整数の秒数。端数は切り上げる (残り 0.2 秒なら 1 秒) ので、
        /// 00:00 になるのは時間切れのときだけ。負・NaN は 0、<see cref="MaxDisplayedSeconds"/> を超える値 (無限大を含む) はそこで止める。
        /// </summary>
        public static int DisplayedSeconds(float remainingSeconds)
        {
            if (!(remainingSeconds > 0f))
            {
                return 0;
            }

            if (remainingSeconds >= MaxDisplayedSeconds)
            {
                return MaxDisplayedSeconds;
            }

            return (int)Math.Ceiling(remainingSeconds);
        }

        /// <example><c>Time(65f)</c> は <c>01:05</c>、<c>Time(0.2f)</c> は <c>00:01</c>、<c>Time(0f)</c> は <c>00:00</c></example>
        public static string Time(float remainingSeconds)
        {
            var total = DisplayedSeconds(remainingSeconds);
            var minutes = total / 60;
            var seconds = total % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + seconds.ToString("00", CultureInfo.InvariantCulture);
        }

        /// <summary>試合が終わった後、次の試合が始まるまでの案内。秒数は整数 (同期されている切り上げの値) で受ける。</summary>
        /// <example><c>NextMatch(10)</c> は <c>次の試合まで 10 秒</c></example>
        public static string NextMatch(int seconds)
        {
            if (seconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative");
            }

            return "次の試合まで " + seconds.ToString(CultureInfo.InvariantCulture) + " 秒";
        }
    }
}

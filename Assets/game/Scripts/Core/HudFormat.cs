using System;

namespace Fps.Core
{
    /// <summary>
    /// HUD に出す数の判断 (表示する秒数・得点・蘇生の百分率、体力が少ないか、相手のチーム)。画面・ログ・通信で同じ数に揃えるため Core に置く。
    /// 文にするのは UI (<c>HudText</c>・<c>HudNumberFormat</c>) が行う。
    /// </summary>
    public static class HudFormat
    {
        /// <summary>mm:ss で表せる最大の秒数 (99:59)。これより長い残り時間は 99:59 に切り詰める。</summary>
        public const int MaxDisplayedSeconds = 99 * 60 + 59;

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

        /// <summary>
        /// 体力が少ないと見なす割合。最大体力のこの割合以下 (0 は除く) で HUD が体力を警告の色にする。
        /// </summary>
        public const float LowHealthRatio = 0.25f;

        /// <summary>体力が警告の色で出すほど少ないか。倒れている (0) ときと最大体力を超えるときは false。</summary>
        /// <exception cref="ArgumentOutOfRangeException">current が負か、max が 1 未満。</exception>
        public static bool IsLowHealth(int current, int max)
        {
            if (current < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(current), current, "current must not be negative");
            }

            if (max < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(max), max, "max must be at least 1");
            }

            return current > 0 && current <= max * LowHealthRatio;
        }

        /// <summary>自分のチームの相手。点数を自分のチーム・相手の順に並べるのに使う。</summary>
        public static Team Opponent(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return Team.Bravo;
                case Team.Bravo:
                    return Team.Alpha;
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }

        /// <summary>
        /// 蘇生の進み (0〜1) を、HUD に出す百分率の整数にする。端数は切り捨てる (100% になるのは起き上がったときだけ)。
        /// 負・NaN は 0、1 を超える値は 100。
        /// </summary>
        /// <example><c>RevivePercent(0.426f)</c> は <c>42</c></example>
        public static int RevivePercent(float progress)
        {
            if (!(progress > 0f))
            {
                return 0;
            }

            if (progress >= 1f)
            {
                return 100;
            }

            return Math.Min(99, (int)Math.Floor(progress * 100f));
        }

        /// <summary>
        /// HUD に出す得点。拠点のように時間で入る点は小数になるので、届いていない点を出さないよう切り捨てる。
        /// 負と NaN は 0、int に収まらない値は <see cref="int.MaxValue"/> にする。
        /// </summary>
        public static int DisplayedScore(double score)
        {
            if (!(score > 0d))
            {
                return 0;
            }

            var floored = Math.Floor(score);
            return floored >= int.MaxValue ? int.MaxValue : (int)floored;
        }

        /// <summary>
        /// 能力の待ち時間を、表示する 0.1 秒の数にする。切り上げる (待っている間に 0.0 を出さない)。
        /// </summary>
        /// <example><c>AbilityWaitTenths(3.42f)</c> は <c>35</c></example>
        /// <exception cref="ArgumentOutOfRangeException">seconds が負か有限でない。</exception>
        public static int AbilityWaitTenths(float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must not be negative and must be finite");
            }

            // float の誤差で 3.0 が 3.0000002 になり 3.1 と出ないよう、切り上げる前に小さく丸める。
            return (int)Math.Ceiling(Math.Round(seconds * 10.0, 3));
        }
    }
}

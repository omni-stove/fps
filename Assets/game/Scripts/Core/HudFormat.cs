using System;

namespace Shooter.Core
{
    // 表示する数の判断を UI でなく Core に置くのは、画面・ログ・通信で同じ数に揃えるため。
    public static class HudFormat
    {
        // mm:ss の 2 桁の分で表せる最大 (99:59) にしてある。
        public const int MaxDisplayedSeconds = 99 * 60 + 59;

        // 端数を切り上げるのは、00:00 を出すのを時間切れのときだけにするため。
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

        public const float LowHealthRatio = 0.25f;

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

        // 端数を切り捨てて 99 で止めるのは、100% を出すのを起き上がったときだけにするため。
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

        // 切り捨てるのは、拠点のように時間で入る点が小数になり、四捨五入では届いていない点を出してしまうため。
        public static int DisplayedScore(double score)
        {
            if (!(score > 0d))
            {
                return 0;
            }

            var floored = Math.Floor(score);
            return floored >= int.MaxValue ? int.MaxValue : (int)floored;
        }

        // 切り上げるのは、待っている間に 0.0 を出さないため。
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

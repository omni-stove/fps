using System;
using System.Globalization;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 拠点制圧 (<see cref="Domination"/>) の <c>[Domination]</c> 付きログの文言。key=value を空白で区切り、値に空白を入れない。
    /// ポイントは <see cref="DominationHudFormat.DisplayedScore"/> で切り捨てた整数、拠点は <see cref="DominationHudFormat.PointName"/> の呼び名で書く。
    /// </summary>
    public static class DominationLog
    {
        public const string Tag = "[Domination]";

        /// <summary>引き分けで終わったときに winner= に書く値。</summary>
        public const string Draw = MatchLog.Draw;

        /// <summary>試合の状態とポイント・残り時間 (秒、小数 1 桁)。</summary>
        /// <example><c>[Domination] state=Playing A=0 B=0 remaining=300.0</c></example>
        public static string State(Domination domination)
        {
            if (domination == null)
            {
                throw new ArgumentNullException(nameof(domination));
            }

            return Tag + " state=" + domination.State
                + Scores(domination)
                + " remaining=" + domination.RemainingSeconds.ToString("F1", CultureInfo.InvariantCulture);
        }

        /// <summary>試合が終わったときの勝者と最終のポイント。</summary>
        /// <example><c>[Domination] ended winner=Alpha A=100 B=37</c>、引き分けなら <c>winner=draw</c></example>
        public static string Ended(Domination domination)
        {
            if (domination == null)
            {
                throw new ArgumentNullException(nameof(domination));
            }

            var winner = domination.Winner;
            return Tag + " ended winner=" + (winner.HasValue ? TeamName(winner.Value) : Draw) + Scores(domination);
        }

        /// <example><c>[Domination] captured point=A team=Alpha</c></example>
        public static string Captured(int pointIndex, Team team)
        {
            return Tag + " captured point=" + DominationHudFormat.PointName(pointIndex) + " team=" + TeamName(team);
        }

        /// <summary>持ち主のいた拠点が中立に戻った。from はそれまでの持ち主。</summary>
        /// <example><c>[Domination] neutralized point=A from=Bravo</c></example>
        public static string Neutralized(int pointIndex, Team previousOwner)
        {
            return Tag + " neutralized point=" + DominationHudFormat.PointName(pointIndex) + " from=" + TeamName(previousOwner);
        }

        /// <summary>試合に加えたプレイヤー。name はログで見分けるための名前 (空なら <c>-</c>、空白は <c>_</c> にする)。</summary>
        /// <example><c>[Domination] joined player=1 team=Bravo name=Enemy-1</c></example>
        public static string Joined(int playerId, Team team, string name)
        {
            return Tag + " joined player=" + Id(playerId) + " team=" + TeamName(team) + " name=" + Token(name);
        }

        /// <example><c>[Domination] kill killer=0 victim=1</c></example>
        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + Id(killerId) + " victim=" + Id(victimId);
        }

        /// <summary>倒した相手のいない死 (自滅・帰属の無いダメージ)。</summary>
        /// <example><c>[Domination] death player=1</c></example>
        public static string Death(int playerId)
        {
            return Tag + " death player=" + Id(playerId);
        }

        /// <summary>倒れたプレイヤーを自チームの出現点へ戻した。at は足元の位置 (小数 2 桁)。</summary>
        /// <example><c>[Domination] respawn player=1 team=Bravo at=(0.00, 0.00, 20.00)</c></example>
        public static string Respawn(int playerId, Team team, Vector3 at)
        {
            return Tag + " respawn player=" + Id(playerId) + " team=" + TeamName(team)
                + " at=(" + at.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + at.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + at.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        /// <example><c>[Domination] restart</c></example>
        public static string Restart()
        {
            return Tag + " restart";
        }

        private static string Scores(Domination domination)
        {
            return " A=" + Id(DominationHudFormat.DisplayedScore(domination.GetScore(Team.Alpha)))
                + " B=" + Id(DominationHudFormat.DisplayedScore(domination.GetScore(Team.Bravo)));
        }

        private static string Id(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        private static string Token(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return "-";
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

        private static string TeamName(Team team)
        {
            switch (team)
            {
                case Team.Alpha:
                    return "Alpha";
                case Team.Bravo:
                    return "Bravo";
                default:
                    throw new ArgumentOutOfRangeException(nameof(team), team, "team is not a valid Team");
            }
        }
    }
}

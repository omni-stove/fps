using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// オフラインのモード (<see cref="ModeSession"/> をシーンで進めるもの) の <c>[Mode]</c> 付きログの文言。
    /// 陣営は ID の数字で書き、得点は小数第 2 位まで (整数ならそのまま) 書く。
    /// </summary>
    public static class ModeLog
    {
        public const string Tag = "[Mode]";

        /// <summary>引き分けで終わったときに winner= に書く値。</summary>
        public const string Draw = "draw";

        /// <summary>
        /// 状態・残り時間・陣営ごとの得点。終了後は勝った陣営も書く。
        /// </summary>
        /// <example><c>[Mode] state=Playing remaining=12.3 score=0:3,1:2</c>、終了後は <c>[Mode] state=Ended remaining=0.0 score=0:10,1:2 winner=0</c></example>
        public static string State(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var text = new StringBuilder(Tag)
                .Append(" state=").Append(session.State)
                .Append(" remaining=").Append(session.RemainingSeconds.ToString("F1", CultureInfo.InvariantCulture))
                .Append(" score=");
            AppendScores(text, session, ":", ",");
            if (session.State == GameModeState.Ended)
            {
                text.Append(" winner=").Append(WinnerName(session.Winner));
            }

            return text.ToString();
        }

        /// <example><c>[Mode] score 0=3 1=2 2=0</c></example>
        public static string Score(ModeSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var text = new StringBuilder(Tag).Append(" score ");
            AppendScores(text, session, "=", " ");
            return text.ToString();
        }

        /// <example><c>[Mode] joined player=0 name=Player side=0</c></example>
        public static string Joined(int playerId, string name, Side side)
        {
            return Tag + " joined player=" + Number(playerId) + " name=" + (string.IsNullOrEmpty(name) ? "-" : name) + " side=" + Number(side.Id);
        }

        /// <example><c>[Mode] kill killer=0 victim=2</c></example>
        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + Number(killerId) + " victim=" + Number(victimId);
        }

        /// <summary>同じ陣営に倒された (点の入らない) ことを、<see cref="Kill"/> と区別して書く。</summary>
        /// <example><c>[Mode] teamkill killer=1 victim=2</c></example>
        public static string TeamKill(int killerId, int victimId)
        {
            return Tag + " teamkill killer=" + Number(killerId) + " victim=" + Number(victimId);
        }

        /// <summary>倒した者が分からない (落下・自滅など) 倒れ方。</summary>
        /// <example><c>[Mode] death player=2</c></example>
        public static string Death(int playerId)
        {
            return Tag + " death player=" + Number(playerId);
        }

        /// <example><c>[Mode] spawn player=2 side=1 at=(4.00, 0.00, -6.00)</c></example>
        public static string Spawn(int playerId, Side side, Vector3 position)
        {
            return Tag + " spawn player=" + Number(playerId) + " side=" + Number(side.Id)
                + " at=(" + position.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + position.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + position.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        /// <example><c>[Mode] ended winner=1</c>、引き分けなら <c>[Mode] ended winner=draw</c></example>
        public static string Ended(Side? winner)
        {
            return Tag + " ended winner=" + WinnerName(winner);
        }

        /// <example><c>[Mode] restart</c></example>
        public static string Restart()
        {
            return Tag + " restart";
        }

        /// <summary>ログに書く得点。整数ならそのまま、そうでなければ小数第 2 位まで。</summary>
        /// <example><c>FormatScore(3d)</c> は <c>3</c>、<c>FormatScore(2.345d)</c> は <c>2.35</c></example>
        public static string FormatScore(double score)
        {
            return score.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static void AppendScores(StringBuilder text, ModeSession session, string pairSeparator, string itemSeparator)
        {
            var sides = session.Sides;
            for (var i = 0; i < sides.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(itemSeparator);
                }

                text.Append(Number(sides[i].Id)).Append(pairSeparator).Append(FormatScore(session.GetScore(sides[i])));
            }
        }

        private static string WinnerName(Side? winner)
        {
            return winner.HasValue ? Number(winner.Value.Id) : Draw;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Shooter.Core
{
    public static class ModeLog
    {
        public const string Tag = "[Mode]";

        public const string Draw = "draw";

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

        public static string Joined(int playerId, string name, Side side)
        {
            return Tag + " joined player=" + Number(playerId) + " name=" + (string.IsNullOrEmpty(name) ? "-" : name) + " side=" + Number(side.Id);
        }

        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + Number(killerId) + " victim=" + Number(victimId);
        }

        public static string Kill(int killerId, int victimId, IReadOnlyList<int> assisterIds)
        {
            var text = Kill(killerId, victimId);
            if (assisterIds == null || assisterIds.Count == 0)
            {
                return text;
            }

            var builder = new StringBuilder(text).Append(" assists=");
            for (var i = 0; i < assisterIds.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(',');
                }

                builder.Append(Number(assisterIds[i]));
            }

            return builder.ToString();
        }

        public static string Stats(ModeSession session, int playerId)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            return Tag + " stats player=" + Number(playerId)
                + " kills=" + Number(session.GetKills(playerId))
                + " deaths=" + Number(session.GetDeaths(playerId))
                + " assists=" + Number(session.GetAssists(playerId));
        }

        // 点の入る Kill と区別できるよう、同じ陣営に倒されたことは別の語で書く。
        public static string TeamKill(int killerId, int victimId)
        {
            return Tag + " teamkill killer=" + Number(killerId) + " victim=" + Number(victimId);
        }

        public static string Death(int playerId)
        {
            return Tag + " death player=" + Number(playerId);
        }

        public static string Spawn(int playerId, Side side, Vector3 position)
        {
            return Tag + " spawn player=" + Number(playerId) + " side=" + Number(side.Id)
                + " at=(" + position.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + position.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + position.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        // 倒されて戻るときだけ、ルールの RespawnAnchor のどれを使ったかを足す。ラウンドの並べ直しは拠点に決まっているので書かない。
        public static string Spawn(int playerId, Side side, Vector3 position, RespawnAnchor anchor)
        {
            if (!Enum.IsDefined(typeof(RespawnAnchor), anchor))
            {
                throw new ArgumentOutOfRangeException(nameof(anchor), anchor, "anchor is not a defined RespawnAnchor");
            }

            return Spawn(playerId, side, position) + " from=" + anchor;
        }

        public static string Ended(Side? winner)
        {
            return Tag + " ended winner=" + WinnerName(winner);
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

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

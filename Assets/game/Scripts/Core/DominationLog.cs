using System;
using System.Globalization;
using System.Numerics;

namespace Shooter.Core
{
    public static class DominationLog
    {
        public const string Tag = "[Domination]";

        public const string Draw = MatchLog.Draw;

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

        public static string Ended(Domination domination)
        {
            if (domination == null)
            {
                throw new ArgumentNullException(nameof(domination));
            }

            var winner = domination.Winner;
            return Tag + " ended winner=" + (winner.HasValue ? TeamName(winner.Value) : Draw) + Scores(domination);
        }

        public static string Captured(int pointIndex, Team team)
        {
            return Tag + " captured point=" + DominationHudFormat.PointName(pointIndex) + " team=" + TeamName(team);
        }

        public static string Neutralized(int pointIndex, Team previousOwner)
        {
            return Tag + " neutralized point=" + DominationHudFormat.PointName(pointIndex) + " from=" + TeamName(previousOwner);
        }

        public static string Joined(int playerId, Team team, string name)
        {
            return Tag + " joined player=" + Id(playerId) + " team=" + TeamName(team) + " name=" + Token(name);
        }

        public static string Kill(int killerId, int victimId)
        {
            return Tag + " kill killer=" + Id(killerId) + " victim=" + Id(victimId);
        }

        public static string Death(int playerId)
        {
            return Tag + " death player=" + Id(playerId);
        }

        public static string Respawn(int playerId, Team team, Vector3 at)
        {
            return Tag + " respawn player=" + Id(playerId) + " team=" + TeamName(team)
                + " at=(" + at.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + at.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + at.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

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

        // key=value を空白で区切るので、値の空白は _ にして途切れないようにする。
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

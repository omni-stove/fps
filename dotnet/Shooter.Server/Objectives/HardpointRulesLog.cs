using System;
using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class HardpointRulesLog
    {
        public const string Tag = "[Hardpoint]";

        public static string Started(HardpointSpec spec)
        {
            ArgumentNullException.ThrowIfNull(spec);
            return Tag + " net points=" + Text(spec.PointCount)
                + " rotateSeconds=" + Text(spec.RotateSeconds)
                + " pointsPerSecond=" + Text(spec.PointsPerSecond)
                + " scoreLimit=" + Text(spec.ScoreLimit)
                + " timeLimit=" + Text(spec.TimeLimitSeconds)
                + " respawn=" + Text(spec.RespawnSeconds);
        }

        public static string Moved(int previousPointIndex, int nextPointIndex)
        {
            return Tag + " moved from=" + DominationHudFormat.PointName(previousPointIndex)
                + " to=" + DominationHudFormat.PointName(nextPointIndex);
        }

        public static string Status(int pointIndex, HardpointStatus status, Side? holder)
        {
            return Tag + " point=" + DominationHudFormat.PointName(pointIndex)
                + " status=" + status
                + " holder=" + TeamName(Side.ToTeam(holder));
        }

        public static string Restart()
        {
            return Tag + " restart";
        }

        public static string State(Hardpoint hardpoint)
        {
            ArgumentNullException.ThrowIfNull(hardpoint);
            var session = hardpoint.Session;
            var text = Tag + " state=" + session.State
                + " point=" + DominationHudFormat.PointName(hardpoint.ActivePoint)
                + " A=" + Text(HudFormat.DisplayedScore(session.GetScore(Side.Alpha)))
                + " B=" + Text(HudFormat.DisplayedScore(session.GetScore(Side.Bravo)))
                + " remaining=" + Math.Max(0f, session.RemainingSeconds).ToString("F1", CultureInfo.InvariantCulture);
            return session.State == GameModeState.Ended ? text + " winner=" + WinnerName(Side.ToTeam(session.Winner)) : text;
        }

        private static string TeamName(Team? team) => team.HasValue ? team.Value.ToString() : "-";

        private static string WinnerName(Team? team) => team.HasValue ? team.Value.ToString() : MatchLog.Draw;

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Text(float value) => value.ToString(CultureInfo.InvariantCulture);
    }
}

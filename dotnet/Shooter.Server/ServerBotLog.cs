using System.Globalization;
using System.Numerics;

namespace Shooter.Server
{
    internal static class ServerBotLog
    {
        public static string Goal(int playerId, Vector3 goal)
        {
            return ServerBots.LogTag + " goal player=" + Text(playerId) + " x=" + F2(goal.X) + " y=" + F2(goal.Y) + " z=" + F2(goal.Z);
        }

        public static string GoalCleared(int playerId)
        {
            return ServerBots.LogTag + " goal player=" + Text(playerId) + " cleared";
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}

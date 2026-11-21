using System.Globalization;
using System.Numerics;
using Shooter.Core;

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

        public static string CoverFound(int playerId, int points)
        {
            return ServerBots.LogTag + " cover player=" + Text(playerId) + " points=" + Text(points);
        }

        public static string GrenadeThrown(int playerId, EnemyState state, Vector3 target)
        {
            return ServerBots.LogTag + " grenade player=" + Text(playerId) + " state=" + state
                + " x=" + F2(target.X) + " y=" + F2(target.Y) + " z=" + F2(target.Z);
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string F2(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}

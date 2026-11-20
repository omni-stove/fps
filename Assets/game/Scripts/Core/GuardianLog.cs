using System.Globalization;

namespace Shooter.Core
{
    public static class GuardianLog
    {
        public static string LeaderChosenLog(Side side, int playerId)
        {
            return ModeLog.Tag + " leader side=" + Number(side.Id) + " player=" + Number(playerId);
        }

        public static string LeaderFellLog(Side side, int playerId)
        {
            return ModeLog.Tag + " leader-down side=" + Number(side.Id) + " player=" + Number(playerId) + " respawn=off";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

using System.Globalization;
using Shooter.Core;

namespace Shooter.NetV2
{
    public static class ClientGuardianLog
    {
        public static string MarkerShown(int playerId, Side side)
        {
            return MatchClient.LogTag + " guardian leader marker shown player=" + Number(playerId) + " side=" + Number(side.Id);
        }

        public static string MarkerRemoved(int playerId)
        {
            return MatchClient.LogTag + " guardian leader marker removed player=" + Number(playerId);
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

using System.Globalization;
using Shooter.Core;

namespace Shooter.Server
{
    internal static class ServerKillCamLog
    {
        public const string Tag = KillCamLog.Tag;

        public static string Settings(float seconds, DeathCamera deathCamera)
        {
            return Tag + " server seconds=" + Seconds(seconds) + " deathCamera=" + deathCamera;
        }

        public static string Sent(int killerId, int victimId, int samples, float durationSeconds)
        {
            return Tag + " sent killer=" + Id(killerId) + " victim=" + Id(victimId)
                + " samples=" + samples.ToString(CultureInfo.InvariantCulture) + " duration=" + Seconds(durationSeconds) + "s";
        }

        public static string NoRecord(int killerId, int victimId)
        {
            return Tag + " no record of killer=" + Id(killerId) + " for victim=" + Id(victimId) + "; nothing sent";
        }

        private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);

        private static string Seconds(float value) => value.ToString("F2", CultureInfo.InvariantCulture);
    }
}

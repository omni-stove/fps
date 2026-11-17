using System;
using System.Globalization;

namespace Shooter.Runtime
{
    public static class NetStatsFormat
    {
        public const int WarningPingMs = 150;

        public const int BadPingMs = 300;

        public const string Unknown = "--";

        public static NetPingLevel PingLevel(int? pingMs)
        {
            if (!pingMs.HasValue)
            {
                return NetPingLevel.Good;
            }

            if (pingMs.Value >= BadPingMs)
            {
                return NetPingLevel.Bad;
            }

            return pingMs.Value >= WarningPingMs ? NetPingLevel.Warning : NetPingLevel.Good;
        }

        public static LocalizedText Text(int? pingMs, int? lossPercent, int? serverTick)
        {
            var ping = pingMs.HasValue ? Number(Math.Max(0, pingMs.Value)) : Unknown;
            var loss = lossPercent.HasValue ? Number(Math.Max(0, Math.Min(100, lossPercent.Value))) : Unknown;
            var tick = serverTick.HasValue ? Number(serverTick.Value) : Unknown;
            return new LocalizedText("net.stats.line", ping, loss, tick);
        }

        public static int PingFromRtt(ulong rttMs)
        {
            return rttMs > int.MaxValue ? int.MaxValue : (int)rttMs;
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

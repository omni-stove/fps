using Shooter.Core;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    internal static class ServerRecordsLog
    {
        public static string ReportsOff()
        {
            return ServerReportLog.Tag + " reports are not kept (pass " + ServerLaunch.ReportFileFlag + " <path> to keep them)";
        }

        public static string StatsNotAsked()
        {
            return StatsIngestLog.Off("not asked (pass " + ServerLaunch.StatsFlag + " to send them)");
        }

        public static string TelemetryNotAsked()
        {
            return TelemetryLog.Tag + " not recording: no consent (pass " + ServerLaunch.TelemetryFlag + " to agree)";
        }

        public static string TelemetryNeedsFolder()
        {
            return TelemetryLog.Tag + " " + ServerLaunch.TelemetryFlag + " keeps the telemetry in a file (sink LocalFile); pass "
                + ServerLaunch.TelemetryDirectoryFlag + " <folder>, or set the sink of " + ServerTelemetry.TuningFileName + " to Http or None";
        }

        public static string TelemetryTuning(string path)
        {
            return TelemetryLog.Tag + " tuning " + (path ?? "default");
        }

        public static string LeaderboardTuning(string path)
        {
            return StatsIngestLog.Tag + " tuning " + (path ?? "default");
        }
    }
}

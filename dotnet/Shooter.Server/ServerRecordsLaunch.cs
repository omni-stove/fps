using System;
using System.Collections.Generic;

namespace Shooter.Server
{
    // 通報・統計・テレメトリは、どれも書き込める置き場所か EOS の認証情報が要る。既定で有効にすると、
    // 書けないイメージ (docker の非 root の利用者) や認証情報の無いサーバーが起動できなくなるので、起動の引数で頼んだときだけ使う。
    internal sealed class ServerRecordsLaunch
    {
        public ServerRecordsLaunch(string reportFile, bool sendsStats, bool telemetryConsent, string telemetryDirectory)
        {
            if (telemetryDirectory != null && !telemetryConsent)
            {
                throw new ArgumentException("a telemetry folder is given without the telemetry consent", nameof(telemetryDirectory));
            }

            ReportFile = reportFile;
            SendsStats = sendsStats;
            TelemetryConsent = telemetryConsent;
            TelemetryDirectory = telemetryDirectory;
        }

        public static ServerRecordsLaunch Off { get; } = new ServerRecordsLaunch(null, false, false, null);

        // null なら通報を受けても残さない。
        public string ReportFile { get; }

        public bool SendsStats { get; }

        // サーバーには設定の画面が無いので、運用する人が --telemetry を渡したことを同意とする。
        public bool TelemetryConsent { get; }

        // 手元のファイル (LocalFile) に残すときのフォルダ。Http や None の送り先では使わない。
        public string TelemetryDirectory { get; }

        public static bool TryParse(IReadOnlyList<string> args, out ServerRecordsLaunch records, out string error)
        {
            ArgumentNullException.ThrowIfNull(args);
            records = null;
            if (!TryParsePath(args, ServerLaunch.ReportFileFlag, out var reportFile, out error)
                || !TryParseSwitch(args, ServerLaunch.StatsFlag, out var stats, out error)
                || !TryParseSwitch(args, ServerLaunch.TelemetryFlag, out var telemetry, out error)
                || !TryParsePath(args, ServerLaunch.TelemetryDirectoryFlag, out var telemetryDirectory, out error))
            {
                return false;
            }

            // 置き場所だけを渡して同意を書き忘れると、残しているつもりで何も残らないので断る
            if (telemetryDirectory != null && !telemetry)
            {
                error = ServerLaunch.TelemetryDirectoryFlag + " is where the telemetry is kept; pass " + ServerLaunch.TelemetryFlag
                    + " too to agree to keep it";
                return false;
            }

            records = new ServerRecordsLaunch(reportFile, stats, telemetry, telemetryDirectory);
            return true;
        }

        // 値の先頭の - を断るのは、パスを書き忘れたときに次のフラグをファイル名として読み、思わぬ場所へ書き出さないため (--ban-file と同じ)
        private static bool TryParsePath(IReadOnlyList<string> args, string flag, out string path, out string error)
        {
            path = null;
            for (var i = 0; i < args.Count; i++)
            {
                if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1][0] == '-')
                {
                    error = flag + " requires a path";
                    return false;
                }

                if (path != null)
                {
                    error = flag + " is given more than once";
                    return false;
                }

                path = args[++i];
            }

            error = null;
            return true;
        }

        private static bool TryParseSwitch(IReadOnlyList<string> args, string flag, out bool given, out string error)
        {
            given = false;
            foreach (var arg in args)
            {
                if (!string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (given)
                {
                    error = flag + " is given more than once";
                    return false;
                }

                given = true;
            }

            error = null;
            return true;
        }
    }
}

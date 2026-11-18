using System;

namespace Shooter.Core
{
    // どこで動いた何の版の記録かを 1 行ごとに載せる。セッションの Id は起動ごとに作る乱数で、同じ起動の記録をまとめて読むためだけに使い、人には結びつけない。
    public sealed class TelemetryOrigin
    {
        public const string ClientSource = "client";

        public const string ServerSource = "server";

        public TelemetryOrigin(string source, string session, string version, string platform)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                throw new ArgumentException("source must not be empty", nameof(source));
            }

            if (string.IsNullOrWhiteSpace(session))
            {
                throw new ArgumentException("session must not be empty", nameof(session));
            }

            Source = source.Trim();
            Session = session.Trim();
            Version = version?.Trim() ?? string.Empty;
            Platform = platform?.Trim() ?? string.Empty;
        }

        public string Source { get; }

        public string Session { get; }

        public string Version { get; }

        public string Platform { get; }

        public static string NewSession()
        {
            return Guid.NewGuid().ToString("N");
        }
    }
}

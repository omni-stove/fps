using System;
using System.IO;
using System.Net.Http;

namespace Shooter.Core
{
    public static class TelemetrySinks
    {
        // ファイルの上限の大きさを、送る前にためる行の上限にも使う。送れない間にためる量も、手元に残す量と同じだけに抑えるため。
        public static ITelemetrySink Create(TelemetrySpec spec, string folder, Action<string> log, HttpMessageHandler handler = null)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            switch (spec.Sink)
            {
                case TelemetrySink.None:
                    return null;
                case TelemetrySink.LocalFile:
                    if (string.IsNullOrWhiteSpace(folder))
                    {
                        throw new ArgumentException("folder must not be empty for the LocalFile sink", nameof(folder));
                    }

                    return new JsonLinesTelemetrySink(Path.Combine(folder, spec.FileName), spec.MaxFileBytes, log);
                case TelemetrySink.Http:
                    return new HttpTelemetrySink(new Uri(spec.Endpoint, UriKind.Absolute), spec.MaxFileBytes, log, handler);
                default:
                    throw new ArgumentOutOfRangeException(nameof(spec), spec.Sink, "unknown telemetry sink");
            }
        }
    }
}

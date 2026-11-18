using System;
using System.IO;

namespace Shooter.Core
{
    public enum TelemetrySink
    {
        None,
        LocalFile,
        Http,
    }

    public sealed class TelemetrySpec
    {
        public const TelemetrySink DefaultSink = TelemetrySink.LocalFile;
        public const string DefaultFileName = "telemetry.jsonl";
        public const string DefaultEndpoint = "";
        public const float DefaultFlushIntervalSeconds = 10f;
        public const int DefaultMaxFileBytes = 5 * 1024 * 1024;

        // これより小さいと、クラッシュの 1 件 (スタックトレース込み) が収まらずに切り替わり続ける。
        public const int MinMaxFileBytes = 64 * 1024;

        public const bool DefaultCrashReports = true;
        public const float DefaultSampleRate = 1f;

        public static TelemetrySpec Default { get; } = new TelemetrySpec();

        public TelemetrySpec(
            TelemetrySink sink = DefaultSink,
            string fileName = DefaultFileName,
            string endpoint = DefaultEndpoint,
            float flushIntervalSeconds = DefaultFlushIntervalSeconds,
            int maxFileBytes = DefaultMaxFileBytes,
            bool crashReports = DefaultCrashReports,
            float sampleRate = DefaultSampleRate)
        {
            Sink = SpecGuard.Defined(sink, nameof(sink));
            FileName = CheckFileName(fileName);
            Endpoint = CheckEndpoint(endpoint, sink);
            FlushIntervalSeconds = SpecGuard.PositiveFinite(flushIntervalSeconds, nameof(flushIntervalSeconds));
            MaxFileBytes = SpecGuard.AtLeast(maxFileBytes, MinMaxFileBytes, nameof(maxFileBytes));
            CrashReports = crashReports;
            SampleRate = SpecGuard.Fraction(sampleRate, nameof(sampleRate));
        }

        public TelemetrySink Sink { get; }

        public string FileName { get; }

        public string Endpoint { get; }

        public float FlushIntervalSeconds { get; }

        public int MaxFileBytes { get; }

        public bool CrashReports { get; }

        public float SampleRate { get; }

        public bool IsOff => Sink == TelemetrySink.None;

        // ファイルは保存先のフォルダの直下に置くので、フォルダを含む名前や外へ出る名前は断る。
        private static string CheckFileName(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new ArgumentException("fileName must not be empty", nameof(fileName));
            }

            var trimmed = fileName.Trim();
            if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || trimmed.Contains('/')
                || trimmed.Contains('\\')
                || trimmed == "."
                || trimmed == "..")
            {
                throw new ArgumentException("fileName must be a plain file name without a folder: '" + trimmed + "'", nameof(fileName));
            }

            return trimmed;
        }

        // 送り先の無い Http は何も送れないまま黙るので、読み込みで止める。平文の http は同意した値が経路で読めるので受け付けない。
        private static string CheckEndpoint(string endpoint, TelemetrySink sink)
        {
            var trimmed = string.IsNullOrWhiteSpace(endpoint) ? string.Empty : endpoint.Trim();
            if (sink != TelemetrySink.Http)
            {
                return trimmed;
            }

            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException("endpoint must be an absolute https URL when sink is Http: '" + trimmed + "'", nameof(endpoint));
            }

            return trimmed;
        }
    }
}

using System.Collections.Generic;

namespace Shooter.Core
{
    public static class TelemetryJson
    {
        public const string ResourcePath = "Tuning/telemetry";

        public const string SinkItem = "sink";
        public const string FileNameItem = "fileName";
        public const string EndpointItem = "endpoint";
        public const string FlushIntervalSecondsItem = "flushIntervalSeconds";
        public const string MaxFileBytesItem = "maxFileBytes";
        public const string CrashReportsItem = "crashReports";
        public const string SampleRateItem = "sampleRate";

        private const string _kind = "telemetry JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            SinkItem,
            FileNameItem,
            EndpointItem,
            FlushIntervalSecondsItem,
            MaxFileBytesItem,
            CrashReportsItem,
            SampleRateItem,
        };

        public static TelemetrySpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var sink = fields.ReadEnum(SinkItem, TelemetrySpec.DefaultSink);
                var fileName = fields.ReadString(FileNameItem, TelemetrySpec.DefaultFileName);
                var endpoint = fields.ReadString(EndpointItem, TelemetrySpec.DefaultEndpoint);
                var flushIntervalSeconds = fields.ReadFloat(FlushIntervalSecondsItem, TelemetrySpec.DefaultFlushIntervalSeconds);
                var maxFileBytes = fields.ReadInt(MaxFileBytesItem, TelemetrySpec.DefaultMaxFileBytes);
                var crashReports = fields.ReadBool(CrashReportsItem, TelemetrySpec.DefaultCrashReports);
                var sampleRate = fields.ReadFloat(SampleRateItem, TelemetrySpec.DefaultSampleRate);
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new TelemetrySpec(sink, fileName, endpoint, flushIntervalSeconds, maxFileBytes, crashReports, sampleRate));
            });
        }
    }
}

using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class ReplayJson
    {
        public const string ResourcePath = "Tuning/replay";

        public const string FreeSpeedItem = "freeSpeed";

        private const string _kind = "replay JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            FreeSpeedItem,
        };

        public static ReplaySpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new ReplaySpec(fields.ReadFloat(FreeSpeedItem, ReplaySpec.DefaultFreeSpeed));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(string.Equals(e.ParamName, FreeSpeedItem, StringComparison.Ordinal) ? e.ParamName : null, e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }
    }
}

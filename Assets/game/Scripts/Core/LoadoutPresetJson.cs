using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public static class LoadoutPresetJson
    {
        public const string ResourcePath = "Tuning/loadout-presets";

        public const string MaxPresetsItem = "maxPresets";

        public const string MaxNameLengthItem = "maxNameLength";

        private const string _kind = "loadout presets JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            MaxPresetsItem,
            MaxNameLengthItem,
        };

        public static LoadoutPresetSpec Parse(string text, string sourceName)
        {
            var source = ServerDataJson.SourceOf(sourceName, _kind);
            try
            {
                var fields = new ServerDataJson.Fields(source, _kind, ServerDataJson.ParseDocument(text, source, _kind), string.Empty, ItemNames);
                try
                {
                    return new LoadoutPresetSpec(
                        fields.ReadInt(MaxPresetsItem, LoadoutPresetSpec.DefaultMaxPresets),
                        fields.ReadInt(MaxNameLengthItem, LoadoutPresetSpec.DefaultMaxNameLength));
                }
                catch (ArgumentException e)
                {
                    throw fields.Rejected(ItemOf(e.ParamName), e.Message, e);
                }
            }
            catch (ServerDataJsonException e)
            {
                // KitTuning は TuningJsonException だけを「読めないので既定値で続ける」と扱う。
                var inner = e.InnerException is ArgumentException rejected ? (Exception)rejected : e;
                throw new TuningJsonException(e.SourceName, e.Item, e.Message, inner);
            }
        }

        private static string ItemOf(string paramName)
        {
            foreach (var item in ItemNames)
            {
                if (string.Equals(item, paramName, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }
    }
}

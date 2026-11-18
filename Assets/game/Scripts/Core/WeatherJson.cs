using System.Collections.Generic;

namespace Shooter.Core
{
    public static class WeatherJson
    {
        public const string ResourcePath = "Tuning/weather";

        public const string PresetsItem = "presets";
        public const string DefaultPresetItem = "defaultPreset";

        public const string IdItem = "id";
        public const string NameKeyItem = "nameKey";
        public const string TimeOfDayHoursItem = "timeOfDayHours";
        public const string FogDensityItem = "fogDensity";
        public const string RainIntensityItem = "rainIntensity";
        public const string WindStrengthItem = "windStrength";
        public const string SunIntensityItem = "sunIntensity";

        private const string _kind = "weather JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[] { PresetsItem, DefaultPresetItem };

        public static IReadOnlyList<string> PresetItemNames { get; } = new[]
        {
            IdItem,
            NameKeyItem,
            TimeOfDayHoursItem,
            FogDensityItem,
            RainIntensityItem,
            WindStrengthItem,
            SunIntensityItem,
        };

        public static WeatherSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var presets = SpecJsonReader.ReadList(fields, PresetsItem, PresetItemNames, ReadPreset);
                var defaultPreset = fields.ReadString(DefaultPresetItem, null);
                return SpecJsonReader.Build(fields, ItemNames, () => new WeatherSpec(presets, defaultPreset));
            });
        }

        // 天候の項目はどれも一緒に決めて初めて 1 つの空になるので、既定値で埋めずにすべて書かせる。
        private static WeatherPreset ReadPreset(ServerDataJson.Fields entry)
        {
            var id = SpecJsonReader.RequireText(entry, IdItem);
            var nameKey = entry.ReadString(NameKeyItem, null);
            var timeOfDayHours = RequireFloat(entry, TimeOfDayHoursItem);
            var fogDensity = RequireFloat(entry, FogDensityItem);
            var rainIntensity = RequireFloat(entry, RainIntensityItem);
            var windStrength = RequireFloat(entry, WindStrengthItem);
            var sunIntensity = RequireFloat(entry, SunIntensityItem);
            return new WeatherPreset(id, nameKey, timeOfDayHours, fogDensity, rainIntensity, windStrength, sunIntensity);
        }

        private static float RequireFloat(ServerDataJson.Fields entry, string name)
        {
            return entry.ToFloat(entry.ItemPath(name), entry.Require(name));
        }
    }
}

using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class WeatherSpec
    {
        public const string ClearNoonId = "clear-noon";
        public const string OvercastDuskId = "overcast-dusk";
        public const string StormNightId = "storm-night";

        public const string DefaultPresetId = ClearNoonId;

        public static IReadOnlyList<WeatherPreset> DefaultPresets { get; } = new[]
        {
            new WeatherPreset(ClearNoonId, "weather.clear-noon", 12f, 0f, 0f, 0.1f, 1f),
            new WeatherPreset(OvercastDuskId, "weather.overcast-dusk", 18.5f, 0.02f, 0f, 0.4f, 0.4f),
            new WeatherPreset(StormNightId, "weather.storm-night", 23f, 0.05f, 1f, 1f, 0.05f),
        };

        public static WeatherSpec Default { get; } = new WeatherSpec();

        public WeatherSpec(IReadOnlyList<WeatherPreset> presets = null, string defaultPreset = null)
        {
            Presets = presets == null ? DefaultPresets : SpecLists.UniqueById(presets, p => p.Id, nameof(presets));
            if (Presets.Count == 0)
            {
                throw new ArgumentException("presets must have at least one preset, since a map always needs a sky", nameof(presets));
            }

            // 書かなければ先頭を使うので、既定の Id を消した一覧でも読める。
            var chosen = defaultPreset == null
                ? SpecLists.FindById(Presets, p => p.Id, DefaultPresetId) ?? Presets[0]
                : SpecLists.FindById(Presets, p => p.Id, defaultPreset.Trim());
            DefaultPreset = chosen ?? throw new ArgumentException(
                "defaultPreset '" + defaultPreset + "' is not one of the presets (" + string.Join(", ", Ids(Presets)) + ")", nameof(defaultPreset));
        }

        public IReadOnlyList<WeatherPreset> Presets { get; }

        public WeatherPreset DefaultPreset { get; }

        public WeatherPreset Find(string id)
        {
            return SpecLists.FindById(Presets, p => p.Id, id);
        }

        // マップのルールが知らない Id を選んでも試合は止めず、既定の天候で続ける。
        public WeatherPreset FindOrDefault(string id)
        {
            return Find(id) ?? DefaultPreset;
        }

        private static IEnumerable<string> Ids(IReadOnlyList<WeatherPreset> presets)
        {
            foreach (var preset in presets)
            {
                yield return preset.Id;
            }
        }
    }

    public sealed class WeatherPreset
    {
        public const float HoursPerDay = 24f;

        public WeatherPreset(string id, string nameKey, float timeOfDayHours, float fogDensity, float rainIntensity, float windStrength, float sunIntensity)
        {
            Id = SpecLists.Id(id, nameof(id));
            DisplayNameKey = SpecGuard.NameKey(nameKey);
            if (!(timeOfDayHours >= 0f && timeOfDayHours < HoursPerDay))
            {
                throw new ArgumentOutOfRangeException(nameof(timeOfDayHours), timeOfDayHours, nameof(timeOfDayHours) + " must be at least 0 and less than 24");
            }

            TimeOfDayHours = timeOfDayHours;
            FogDensity = SpecGuard.Fraction(fogDensity, nameof(fogDensity));
            RainIntensity = SpecGuard.Fraction(rainIntensity, nameof(rainIntensity));
            WindStrength = SpecGuard.Fraction(windStrength, nameof(windStrength));
            SunIntensity = SpecGuard.NonNegativeFinite(sunIntensity, nameof(sunIntensity));
        }

        public string Id { get; }

        public string DisplayNameKey { get; }

        public float TimeOfDayHours { get; }

        public float FogDensity { get; }

        public float RainIntensity { get; }

        public float WindStrength { get; }

        public float SunIntensity { get; }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeatherJsonTests
    {
        private const string _source = "weather.json";

        private const string _fog =
            "{\"id\": \"fog\", \"nameKey\": \"w.fog\", \"timeOfDayHours\": 6.5, \"fogDensity\": 0.08, \"rainIntensity\": 0.2, \"windStrength\": 0.3, \"sunIntensity\": 0.6}";

        private const string _night =
            "{\"id\": \"night\", \"timeOfDayHours\": 0, \"fogDensity\": 0, \"rainIntensity\": 0, \"windStrength\": 0, \"sunIntensity\": 0}";

        private static readonly string[] _expectedItemNames = { "presets", "defaultPreset" };

        private static readonly string[] _expectedPresetItemNames =
        {
            "id", "nameKey", "timeOfDayHours", "fogDensity", "rainIntensity", "windStrength", "sunIntensity",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = WeatherJson.Parse("{\"presets\": [" + _fog + ", " + _night + "], \"defaultPreset\": \"night\"}", _source);

            Assert.That(spec.Presets.Count, Is.EqualTo(2));
            var fog = spec.Presets[0];
            Assert.That(fog.Id, Is.EqualTo("fog"));
            Assert.That(fog.DisplayNameKey, Is.EqualTo("w.fog"));
            Assert.That(fog.TimeOfDayHours, Is.EqualTo(6.5f));
            Assert.That(fog.FogDensity, Is.EqualTo(0.08f));
            Assert.That(fog.RainIntensity, Is.EqualTo(0.2f));
            Assert.That(fog.WindStrength, Is.EqualTo(0.3f));
            Assert.That(fog.SunIntensity, Is.EqualTo(0.6f));
            Assert.That(spec.DefaultPreset, Is.SameAs(spec.Presets[1]));
            Assert.That(spec.FindOrDefault("fog"), Is.SameAs(fog));
            Assert.That(spec.FindOrDefault("sandstorm"), Is.SameAs(spec.Presets[1]), "an unknown map choice falls back to the default");
            Assert.That(spec.FindOrDefault(null), Is.SameAs(spec.Presets[1]));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = WeatherJson.Parse("{}", _source);

            Assert.That(spec.Presets, Is.SameAs(WeatherSpec.DefaultPresets));
            Assert.That(spec.Presets.Count, Is.EqualTo(3));
            Assert.That(spec.DefaultPreset.Id, Is.EqualTo("clear-noon"));
            Assert.That(spec.DefaultPreset.DisplayNameKey, Is.EqualTo("weather.clear-noon"));
            Assert.That(spec.DefaultPreset.TimeOfDayHours, Is.EqualTo(12f));
            Assert.That(spec.DefaultPreset.FogDensity, Is.EqualTo(0f));
            Assert.That(spec.DefaultPreset.RainIntensity, Is.EqualTo(0f));
            Assert.That(spec.DefaultPreset.WindStrength, Is.EqualTo(0.1f));
            Assert.That(spec.DefaultPreset.SunIntensity, Is.EqualTo(1f));
            Assert.That(spec.Find("overcast-dusk").TimeOfDayHours, Is.EqualTo(18.5f));
            Assert.That(spec.Find("overcast-dusk").FogDensity, Is.EqualTo(0.02f));
            Assert.That(spec.Find("overcast-dusk").WindStrength, Is.EqualTo(0.4f));
            Assert.That(spec.Find("overcast-dusk").SunIntensity, Is.EqualTo(0.4f));
            Assert.That(spec.Find("storm-night").TimeOfDayHours, Is.EqualTo(23f));
            Assert.That(spec.Find("storm-night").FogDensity, Is.EqualTo(0.05f));
            Assert.That(spec.Find("storm-night").RainIntensity, Is.EqualTo(1f));
            Assert.That(spec.Find("storm-night").WindStrength, Is.EqualTo(1f));
            Assert.That(spec.Find("storm-night").SunIntensity, Is.EqualTo(0.05f));
        }

        [Test]
        public void WithoutTheDefaultIdTheFirstPresetIsTheDefault()
        {
            var spec = WeatherJson.Parse("{\"presets\": [" + _fog + ", " + _night + "]}", _source);

            Assert.That(spec.DefaultPreset.Id, Is.EqualTo("fog"));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(WeatherJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(WeatherJson.PresetItemNames, Is.EqualTo(_expectedPresetItemNames));
            Assert.That(WeatherJson.ResourcePath, Is.EqualTo("Tuning/weather"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var empty = Reject("{\"presets\": []}");
            var unknownDefault = Reject("{\"presets\": [" + _fog + "], \"defaultPreset\": \"night\"}");
            var unknownBuiltIn = Reject("{\"defaultPreset\": \"sandstorm\"}");
            var hour = Reject("{\"presets\": [" + _fog.Replace("6.5", "24", StringComparison.Ordinal) + "]}");
            var fog = Reject("{\"presets\": [" + _fog.Replace("0.08", "1.5", StringComparison.Ordinal) + "]}");
            var sun = Reject("{\"presets\": [" + _fog.Replace("0.6", "-1", StringComparison.Ordinal) + "]}");
            var missing = Reject("{\"presets\": [{\"id\": \"x\", \"timeOfDayHours\": 1}]}");

            Assert.That(empty.Item, Is.EqualTo("presets"));
            Assert.That(empty.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(unknownDefault.Item, Is.EqualTo("defaultPreset"));
            Assert.That(unknownDefault.Message, Does.Contain("(fog)"), "lists the presets");
            Assert.That(unknownBuiltIn.Item, Is.EqualTo("defaultPreset"));
            Assert.That(hour.Item, Is.EqualTo("presets[0].timeOfDayHours"));
            Assert.That(fog.Item, Is.EqualTo("presets[0].fogDensity"));
            Assert.That(sun.Item, Is.EqualTo("presets[0].sunIntensity"));
            Assert.That(missing.Item, Is.EqualTo("presets[0].fogDensity"), "every sky value is required");
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => WeatherJson.Parse(json, _source));
        }
    }
}

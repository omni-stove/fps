using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class WeatherChoiceTests
    {
        private static readonly WeatherSpec _spec = WeatherSpec.Default;

        [Test]
        public void TheRulesWeatherAndTimeOfDayAreUsed()
        {
            var choice = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, WeatherSpec.StormNightId);

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(choice.TimeOfDay.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(choice.WeatherFromRules, Is.True);
            Assert.That(choice.TimeOfDayFromRules, Is.True);
            Assert.That(choice.RainIntensity, Is.EqualTo(1f));
            Assert.That(choice.FogDensity, Is.EqualTo(0.05f));
            Assert.That(choice.WindStrength, Is.EqualTo(1f));
            Assert.That(choice.TimeOfDayHours, Is.EqualTo(23f));
            Assert.That(choice.SunIntensity, Is.EqualTo(0.05f));
            Assert.That(choice.IsNight, Is.True);
        }

        [Test]
        public void TheWeatherAndTheTimeOfDayComeFromDifferentPresets()
        {
            var choice = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, WeatherSpec.ClearNoonId);

            Assert.That(choice.RainIntensity, Is.EqualTo(1f), "rain comes from the weather");
            Assert.That(choice.FogDensity, Is.EqualTo(0.05f));
            Assert.That(choice.WindStrength, Is.EqualTo(1f));
            Assert.That(choice.TimeOfDayHours, Is.EqualTo(12f), "the hour comes from the time of day");
            Assert.That(choice.SunIntensity, Is.EqualTo(1f));
            Assert.That(choice.IsNight, Is.False);
        }

        [Test]
        public void WithoutATimeOfDayTheWeatherPresetsOwnTimeIsUsed()
        {
            var choice = WeatherChoice.Choose(_spec, WeatherSpec.OvercastDuskId, null);

            Assert.That(choice.TimeOfDay.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
            Assert.That(choice.TimeOfDayFromRules, Is.False);
            Assert.That(choice.TimeOfDayHours, Is.EqualTo(18.5f));
            Assert.That(choice.SunIntensity, Is.EqualTo(0.4f));
        }

        [Test]
        public void UnsetRulesFallBackToTheMapDefaults()
        {
            var choice = WeatherChoice.Choose(_spec, "", "  ", WeatherSpec.OvercastDuskId, WeatherSpec.StormNightId);

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
            Assert.That(choice.TimeOfDay.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(choice.WeatherFromRules, Is.False);
            Assert.That(choice.TimeOfDayFromRules, Is.False);
        }

        [Test]
        public void TheRulesWinOverTheMapDefaults()
        {
            var choice = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, WeatherSpec.StormNightId, WeatherSpec.ClearNoonId, WeatherSpec.ClearNoonId);

            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(choice.TimeOfDay.Id, Is.EqualTo(WeatherSpec.StormNightId));
        }

        [Test]
        public void NothingChosenUsesTheSpecDefault()
        {
            var choice = WeatherChoice.Choose(_spec, null, null);

            Assert.That(choice.Weather, Is.SameAs(_spec.DefaultPreset));
            Assert.That(choice.TimeOfDay, Is.SameAs(_spec.DefaultPreset));
            Assert.That(choice.Weather.Id, Is.EqualTo(WeatherSpec.ClearNoonId));
            Assert.That(choice.RainIntensity, Is.Zero);
        }

        [Test]
        public void UnknownIdsFallBackToTheMapAndThenToTheSpecDefault()
        {
            var toMap = WeatherChoice.Choose(_spec, "sandstorm", "eclipse", WeatherSpec.OvercastDuskId);
            Assert.That(toMap.Weather.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
            Assert.That(toMap.TimeOfDay.Id, Is.EqualTo(WeatherSpec.OvercastDuskId));
            Assert.That(toMap.WeatherFromRules, Is.False);
            Assert.That(toMap.TimeOfDayFromRules, Is.False);

            var toDefault = WeatherChoice.Choose(_spec, "sandstorm", null, "also-unknown");
            Assert.That(toDefault.Weather.Id, Is.EqualTo(WeatherSpec.ClearNoonId));
        }

        [Test]
        public void IdsAreTrimmedAndMatchedExactly()
        {
            Assert.That(WeatherChoice.Choose(_spec, " storm-night ", null).Weather.Id, Is.EqualTo(WeatherSpec.StormNightId));
            Assert.That(WeatherChoice.Choose(_spec, "Storm-Night", null).Weather.Id, Is.EqualTo(WeatherSpec.ClearNoonId), "Ids are case-sensitive like every other content Id");
        }

        [Test]
        public void AGameSpecIsChosenFrom()
        {
            var rain = new WeatherPreset("rain", null, 15f, 0.03f, 0.8f, 0.5f, 0.3f);
            var night = new WeatherPreset("night", null, 0f, 0f, 0f, 0f, 0.02f);
            var spec = new WeatherSpec(new[] { rain, night }, "rain");

            var choice = WeatherChoice.Choose(spec, "rain", "night");

            Assert.That(choice.Weather, Is.SameAs(rain));
            Assert.That(choice.TimeOfDay, Is.SameAs(night));
            Assert.That(choice.RainIntensity, Is.EqualTo(0.8f));
            Assert.That(choice.TimeOfDayHours, Is.Zero);
            Assert.That(choice.SunIntensity, Is.EqualTo(0.02f));
            Assert.That(choice.IsNight, Is.True);
            Assert.That(choice.LightPitchDegrees, Is.EqualTo(90f), "at midnight the moon is straight up");
        }

        [Test]
        public void TheSpecIsRequired()
        {
            Assert.Throws<ArgumentNullException>(() => WeatherChoice.Choose(null, "rain", "night"));
        }

        [TestCase(6f, 0f)]
        [TestCase(9f, 45f)]
        [TestCase(12f, 90f)]
        [TestCase(18f, 180f)]
        [TestCase(18.5f, 187.5f)]
        [TestCase(23f, 255f)]
        [TestCase(0f, 270f)]
        [TestCase(3f, 315f)]
        public void TheSunCrossesTheSkyFromSixToEighteen(float hours, float pitch)
        {
            Assert.That(WeatherChoice.SunPitch(hours), Is.EqualTo(pitch).Within(1e-4f));
        }

        [TestCase(6f, 0f)]
        [TestCase(12f, 90f)]
        [TestCase(18f, 180f)]
        [TestCase(18.5f, 7.5f)]
        [TestCase(23f, 75f)]
        [TestCase(0f, 90f)]
        [TestCase(3f, 135f)]
        public void AtNightTheLightComesFromTheMoonAboveTheHorizon(float hours, float pitch)
        {
            Assert.That(WeatherChoice.LightPitch(hours), Is.EqualTo(pitch).Within(1e-4f));
        }

        [TestCase(5.9f, false)]
        [TestCase(6f, false)]
        [TestCase(6.1f, true)]
        [TestCase(12f, true)]
        [TestCase(17.9f, true)]
        [TestCase(18f, false)]
        [TestCase(23f, false)]
        public void TheSunIsUpBetweenSixAndEighteen(float hours, bool up)
        {
            Assert.That(WeatherChoice.IsSunUp(hours), Is.EqualTo(up));
        }

        [Test]
        public void ChoicesOfTheSamePresetsAreEqual()
        {
            var a = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, null);
            var b = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, null);
            var c = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, WeatherSpec.ClearNoonId);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(c));
        }

        [Test]
        public void TheAppliedLineNamesTheChoiceAndWhatWasAskedFor()
        {
            var choice = WeatherChoice.Choose(_spec, WeatherSpec.StormNightId, null);

            Assert.That(
                WeatherLog.Applied(WeatherLog.RulesSource, choice, " storm-night ", null),
                Is.EqualTo("[Weather] applied from=rules weather=storm-night timeOfDay=storm-night hours=23 fog=0.05 rain=1 wind=1 sun=0.05 requested=storm-night/-"));
            Assert.That(
                WeatherLog.UnknownId("weatherId", "sandstorm", "clear-noon"),
                Is.EqualTo("[Weather] weatherId 'sandstorm' is not in the weather presets; using 'clear-noon' instead"));
        }
    }
}

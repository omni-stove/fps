using NUnit.Framework;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class RecordAndContentTuningFileTests
    {
        [Test]
        public void TheChatWordFileHoldsTheDefaults()
        {
            var spec = ChatWordFilterJson.Parse(Bundled(ChatWordFilterJson.ResourcePath), ChatWordFilterJson.ResourcePath);

            Assert.That(spec.Words, Is.EqualTo(ChatWordFilterSpec.DefaultWords));
            Assert.That(spec.Mask, Is.EqualTo(ChatWordFilterSpec.DefaultMask));
            Assert.That(spec.Action, Is.EqualTo(ChatWordFilterSpec.DefaultAction));
            Assert.That(spec.WholeWordsOnly, Is.EqualTo(ChatWordFilterSpec.DefaultWholeWordsOnly));
        }

        [Test]
        public void TheAchievementFileHoldsTheDefaults()
        {
            var spec = AchievementJson.Parse(Bundled(AchievementJson.ResourcePath), AchievementJson.ResourcePath);
            var defaults = AchievementSpec.DefaultAchievements;

            Assert.That(spec.Achievements.Count, Is.EqualTo(defaults.Count));
            for (var i = 0; i < defaults.Count; i++)
            {
                Assert.That(spec.Achievements[i].Id, Is.EqualTo(defaults[i].Id));
                Assert.That(spec.Achievements[i].DisplayNameKey, Is.EqualTo(defaults[i].DisplayNameKey));
                Assert.That(spec.Achievements[i].DescriptionKey, Is.EqualTo(defaults[i].DescriptionKey));
                Assert.That(spec.Achievements[i].Stat, Is.EqualTo(defaults[i].Stat));
                Assert.That(spec.Achievements[i].Target, Is.EqualTo(defaults[i].Target));
                Assert.That(spec.Achievements[i].Hidden, Is.EqualTo(defaults[i].Hidden));
            }

            Assert.That(spec.MeleeWeaponIds, Is.EqualTo(AchievementSpec.DefaultMeleeWeaponIds));
        }

        [Test]
        public void TheLeaderboardFileHoldsTheDefaults()
        {
            var spec = LeaderboardJson.Parse(Bundled(LeaderboardJson.ResourcePath), LeaderboardJson.ResourcePath);
            var defaults = LeaderboardSpec.DefaultBoards;

            Assert.That(spec.Boards.Count, Is.EqualTo(defaults.Count));
            for (var i = 0; i < defaults.Count; i++)
            {
                Assert.That(spec.Boards[i].Id, Is.EqualTo(defaults[i].Id));
                Assert.That(spec.Boards[i].DisplayNameKey, Is.EqualTo(defaults[i].DisplayNameKey));
                Assert.That(spec.Boards[i].Stat, Is.EqualTo(defaults[i].Stat));
                Assert.That(spec.Boards[i].Aggregation, Is.EqualTo(defaults[i].Aggregation));
                Assert.That(spec.Boards[i].StatName, Is.EqualTo(defaults[i].StatName));
            }

            Assert.That(spec.PageSize, Is.EqualTo(LeaderboardSpec.DefaultPageSize));
            Assert.That(spec.RefreshSeconds, Is.EqualTo(LeaderboardSpec.DefaultRefreshSeconds));
        }

        [Test]
        public void TheCosmeticFileHoldsTheDefaults()
        {
            var catalog = CosmeticJson.Parse(Bundled(CosmeticJson.ResourcePath), CosmeticJson.ResourcePath);

            Assert.That(catalog.Items.Count, Is.EqualTo(CosmeticCatalog.DefaultItems.Count));
        }

        [Test]
        public void TheWeatherFileHoldsTheDefaults()
        {
            var spec = WeatherJson.Parse(Bundled(WeatherJson.ResourcePath), WeatherJson.ResourcePath);
            var defaults = WeatherSpec.DefaultPresets;

            Assert.That(spec.Presets.Count, Is.EqualTo(defaults.Count));
            for (var i = 0; i < defaults.Count; i++)
            {
                Assert.That(spec.Presets[i].Id, Is.EqualTo(defaults[i].Id));
                Assert.That(spec.Presets[i].DisplayNameKey, Is.EqualTo(defaults[i].DisplayNameKey));
                Assert.That(spec.Presets[i].TimeOfDayHours, Is.EqualTo(defaults[i].TimeOfDayHours));
                Assert.That(spec.Presets[i].FogDensity, Is.EqualTo(defaults[i].FogDensity));
                Assert.That(spec.Presets[i].RainIntensity, Is.EqualTo(defaults[i].RainIntensity));
                Assert.That(spec.Presets[i].WindStrength, Is.EqualTo(defaults[i].WindStrength));
                Assert.That(spec.Presets[i].SunIntensity, Is.EqualTo(defaults[i].SunIntensity));
            }

            Assert.That(spec.DefaultPreset.Id, Is.EqualTo(WeatherSpec.DefaultPresetId));
        }

        [Test]
        public void TheTelemetryFileHoldsTheDefaults()
        {
            var spec = TelemetryJson.Parse(Bundled(TelemetryJson.ResourcePath), TelemetryJson.ResourcePath);

            Assert.That(spec.Sink, Is.EqualTo(TelemetrySpec.DefaultSink));
            Assert.That(spec.FileName, Is.EqualTo(TelemetrySpec.DefaultFileName));
            Assert.That(spec.Endpoint, Is.EqualTo(TelemetrySpec.DefaultEndpoint));
            Assert.That(spec.FlushIntervalSeconds, Is.EqualTo(TelemetrySpec.DefaultFlushIntervalSeconds));
            Assert.That(spec.MaxFileBytes, Is.EqualTo(TelemetrySpec.DefaultMaxFileBytes));
            Assert.That(spec.CrashReports, Is.EqualTo(TelemetrySpec.DefaultCrashReports));
            Assert.That(spec.SampleRate, Is.EqualTo(TelemetrySpec.DefaultSampleRate));
        }

        [Test]
        public void TheInfectedFileHoldsTheDefaults()
        {
            var spec = InfectedJson.Parse(Bundled(InfectedJson.ResourcePath), InfectedJson.ResourcePath);

            Assert.That(spec.InitialInfected, Is.EqualTo(InfectedSpec.DefaultInitialInfected));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(InfectedSpec.DefaultStartDelaySeconds));
            Assert.That(spec.RoundSeconds, Is.EqualTo(InfectedSpec.DefaultRoundSeconds));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(InfectedSpec.DefaultInfectedMaxHealth));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(InfectedSpec.DefaultInfectedSpeedMultiplier));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(InfectedSpec.DefaultInfectedRespawnSeconds));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(InfectedSpec.DefaultScorePerInfection));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(InfectedSpec.DefaultScorePerSurvivedRound));
        }

        [Test]
        public void TheLootStashAndExtractionFilesHoldTheDefaults()
        {
            var loot = LootJson.Parse(Bundled(LootJson.ResourcePath), LootJson.ResourcePath);
            var stash = StashJson.Parse(Bundled(StashJson.ResourcePath), StashJson.ResourcePath);
            var extraction = ExtractionJson.Parse(Bundled(ExtractionJson.ResourcePath), ExtractionJson.ResourcePath);

            Assert.That(loot.Tables.Count, Is.EqualTo(LootSpec.DefaultTables.Count));
            Assert.That(stash.Capacity, Is.EqualTo(StashSpec.DefaultCapacity));
            Assert.That(stash.StartingItems.Count, Is.EqualTo(StashSpec.DefaultStartingItems.Count));
            Assert.That(extraction.RaidSeconds, Is.EqualTo(ExtractionSpec.DefaultRaidSeconds));
            Assert.That(extraction.ExtractHoldSeconds, Is.EqualTo(ExtractionSpec.DefaultExtractHoldSeconds));
            Assert.That(extraction.OpenPoints, Is.EqualTo(ExtractionSpec.DefaultOpenPoints));
            Assert.That(extraction.OpenAfterSeconds, Is.EqualTo(ExtractionSpec.DefaultOpenAfterSeconds));
            Assert.That(extraction.KeepCarriedOnDeath, Is.EqualTo(ExtractionSpec.DefaultKeepCarriedOnDeath));
        }

        private static string Bundled(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/" + resourcePath + ".json");
            return asset.text;
        }
    }
}

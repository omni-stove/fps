using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

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
            Assert.That(spec.InfectedWeaponId, Is.EqualTo(InfectedSpec.DefaultInfectedWeaponId));
        }

        [Test]
        public void ThePayloadFileHoldsTheDefaults()
        {
            var spec = PayloadJson.Parse(Bundled(PayloadJson.ResourcePath), PayloadJson.ResourcePath);

            Assert.That(spec.CartSpeed, Is.EqualTo(PayloadSpec.DefaultCartSpeed));
            Assert.That(spec.MinPushers, Is.EqualTo(PayloadSpec.DefaultMinPushers));
            Assert.That(spec.PusherSpeedMultipliers, Is.EqualTo(PayloadSpec.Default.PusherSpeedMultipliers));
            Assert.That(spec.RollbackDelaySeconds, Is.EqualTo(PayloadSpec.DefaultRollbackDelaySeconds));
            Assert.That(spec.RollbackSpeed, Is.EqualTo(PayloadSpec.DefaultRollbackSpeed));
            Assert.That(spec.CheckpointBonusSeconds, Is.EqualTo(PayloadSpec.DefaultCheckpointBonusSeconds));
            Assert.That(spec.TimeLimitSeconds, Is.EqualTo(PayloadSpec.DefaultTimeLimitSeconds));
            Assert.That(KitTuning.PayloadResourcePath, Is.EqualTo(PayloadJson.ResourcePath));
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

        [Test]
        public void TheEconomyFileHoldsTheDefaultAmountsAndSellsWeaponsAndThrowables()
        {
            var rules = EconomyJson.Parse(Bundled(EconomyJson.ResourcePath), EconomyJson.ResourcePath);
            var grenades = TuningJson.ParseGrenades(Bundled(KitTuning.GrenadeResourcePath), KitTuning.GrenadeResourcePath);

            Assert.That(rules.Spec.StartMoney, Is.EqualTo(EconomySpec.DefaultStartMoney));
            Assert.That(rules.Spec.MaxMoney, Is.EqualTo(EconomySpec.DefaultMaxMoney));
            Assert.That(rules.Spec.KillReward, Is.EqualTo(EconomySpec.DefaultKillReward));
            Assert.That(rules.Spec.RoundWinReward, Is.EqualTo(EconomySpec.DefaultRoundWinReward));
            Assert.That(rules.Spec.RoundLossReward, Is.EqualTo(EconomySpec.DefaultRoundLossReward));
            Assert.That(rules.Spec.LossStreakBonus, Is.EqualTo(EconomySpec.DefaultLossStreakBonus));
            Assert.That(rules.Spec.MaxLossReward, Is.EqualTo(EconomySpec.DefaultMaxLossReward));
            Assert.That(rules.ResetOnSwap, Is.EqualTo(EconomyRules.DefaultResetOnSwap));

            var weapons = 0;
            var throwables = 0;
            foreach (var item in rules.Catalog.Items)
            {
                Assert.That(item.Price, Is.LessThanOrEqualTo(rules.Spec.MaxMoney), item.Id + " can be afforded");
                Assert.That(item.NameKey, Is.Not.Empty, item.Id + " has a name key for the buy menu");
                if (item.Kind == BuyItemKind.Weapon)
                {
                    weapons++;
                }
                else if (item.Kind == BuyItemKind.Throwable)
                {
                    throwables++;
                    Assert.That(grenades.TryFind(item.Id, out _), Is.True, item.Id + " is a grenade type in grenade.json");
                }
            }

            Assert.That(weapons, Is.GreaterThan(0));
            Assert.That(throwables, Is.GreaterThan(0));
        }

        private static readonly (string path, Func<GameContent, TextAsset> select, Action<GameContent, TextAsset> set)[] _newTuningSlots =
        {
            (KitTuning.PayloadResourcePath, c => c.PayloadTuning, (c, v) => c.PayloadTuning = v),
            (KitTuning.EconomyResourcePath, c => c.EconomyTuning, (c, v) => c.EconomyTuning = v),
            (KitTuning.SupplyDropsResourcePath, c => c.SupplyDropsTuning, (c, v) => c.SupplyDropsTuning = v),
            (KitTuning.RedeployResourcePath, c => c.RedeployTuning, (c, v) => c.RedeployTuning = v),
            (KitTuning.MutatorsResourcePath, c => c.MutatorsTuning, (c, v) => c.MutatorsTuning = v),
            (KitTuning.PlaylistsResourcePath, c => c.PlaylistsTuning, (c, v) => c.PlaylistsTuning = v),
        };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            GameContent.Use(null);
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheNewTuningPathsAreUnderTuningAndDistinct()
        {
            var paths = new List<string>();
            foreach (var slot in _newTuningSlots)
            {
                Assert.That(slot.path, Does.StartWith("Tuning/"));
                paths.Add(slot.path);
            }

            Assert.That(paths, Is.EqualTo(new[] { "Tuning/payload", "Tuning/economy", "Tuning/supply-drops", "Tuning/redeploy", "Tuning/mutators", "Tuning/playlists" }));
            Assert.That(paths, Is.Unique);
        }

        [Test]
        public void AnEmptyNewTuningSlotReadsTheBundledFileOrTheDefault()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Empty";
            GameContent.Use(content);

            foreach (var slot in _newTuningSlots)
            {
                Assert.That(slot.select(content), Is.Null, slot.path + " starts empty");

                var bundled = Resources.Load<TextAsset>(slot.path);
                var expected = bundled != null ? bundled.text : "default";
                var read = KitTuning.Load(slot.select, slot.path, (text, _) => text, "default");

                Assert.That(read, Is.EqualTo(expected), slot.path + " falls back to Resources, then to the default");
            }
        }

        [Test]
        public void AFilledNewTuningSlotIsReadBeforeResources()
        {
            var content = Track(ScriptableObject.CreateInstance<GameContent>());
            content.name = "Filled";
            foreach (var slot in _newTuningSlots)
            {
                var asset = Track(new TextAsset("{ \"from\": \"" + slot.path + "\" }"));
                asset.name = slot.path.Substring(slot.path.LastIndexOf('/') + 1);
                slot.set(content, asset);
            }

            GameContent.Use(content);

            foreach (var slot in _newTuningSlots)
            {
                var text = KitTuning.FindText(slot.select, slot.path, out var source);

                Assert.That(text, Is.EqualTo("{ \"from\": \"" + slot.path + "\" }"));
                Assert.That(source, Does.Contain("GameContent 'Filled'"));
                Assert.That(KitTuning.Load(slot.select, slot.path, (t, _) => t, "default"), Is.EqualTo(text));
            }
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static string Bundled(string resourcePath)
        {
            var asset = Resources.Load<TextAsset>(resourcePath);
            Assert.That(asset, Is.Not.Null, "the kit ships Resources/" + resourcePath + ".json");
            return asset.text;
        }
    }
}

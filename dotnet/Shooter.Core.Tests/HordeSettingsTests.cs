using System;
using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class HordeSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings, GameRules rules = null)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                "Horde",
                rules ?? GameRules.Default,
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match)
        {
            return "{\"match\": " + match + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultsAreTheOfflineHordeDefaults()
        {
            var settings = HordeSettings.Default;

            Assert.That(settings.StartingCurrency, Is.EqualTo(50));
            Assert.That(settings.KillReward, Is.EqualTo(10));
            Assert.That(settings.KillRewardPerWave, Is.EqualTo(2));
            Assert.That(settings.PurchasesOnlyBetweenWaves, Is.True);
            Assert.That(settings.KillScore, Is.EqualTo(1));
            Assert.That(settings.BuildCost, Is.EqualTo(40));
            Assert.That(settings.RepairCostPerPiece, Is.EqualTo(4));
            Assert.That(settings.InteractRadius, Is.EqualTo(3.5f));
            Assert.That(settings.CorpseSeconds, Is.EqualTo(5f));
            Assert.That(settings.AlertInterval, Is.EqualTo(3f));
            Assert.That(settings.WaveCount, Is.EqualTo(5));
            Assert.That(settings.RestSeconds, Is.EqualTo(5f));
            Assert.That(settings.FirstEnemyCount, Is.EqualTo(3));
            Assert.That(settings.EnemiesPerWave, Is.EqualTo(2f));
            Assert.That(settings.FirstStrength, Is.EqualTo(1f));
            Assert.That(settings.StrengthPerWave, Is.EqualTo(0.25f));
            Assert.That(settings.MaxStrength, Is.EqualTo(3f));
            Assert.That(settings.MaxAlive, Is.EqualTo(4));
            Assert.That(NetMatchSettings.Default.Horde, Is.SameAs(HordeSettings.Default));
        }

        [Test]
        public void TheSpecAndTheWavesComeFromTheSettings()
        {
            var settings = new HordeSettings(startingCurrency: 80, killReward: 7, corpseSeconds: 2f, alertInterval: 1.5f, waveCount: 3, firstEnemyCount: 4, enemiesPerWave: 1f, maxAlive: 3);

            var spec = settings.ToSpec(Side.Alpha, Side.Bravo);
            var schedule = settings.CreateSchedule();

            Assert.That(spec.Defenders, Is.EqualTo(Side.Alpha));
            Assert.That(spec.Attackers, Is.EqualTo(Side.Bravo));
            Assert.That(spec.StartingCurrency, Is.EqualTo(80));
            Assert.That(spec.KillReward, Is.EqualTo(7));
            Assert.That(spec.CorpseSeconds, Is.EqualTo(2f));
            Assert.That(spec.AlertInterval, Is.EqualTo(1.5f));
            Assert.That(spec.MinSpawnDistance, Is.EqualTo(HordeSpec.DefaultMinSpawnDistance));
            Assert.That(schedule.WaveCount, Is.EqualTo(3));
            Assert.That(schedule.WaveAt(1).EnemyCount, Is.EqualTo(4));
            Assert.That(schedule.WaveAt(3).EnemyCount, Is.EqualTo(6));
            Assert.That(schedule.WaveAt(1).MaxAlive, Is.EqualTo(3));
            Assert.That(settings.CreateSchedule(), Is.Not.SameAs(schedule), "each match gets a fresh schedule");
            Assert.That(new HordeSettings(waveCount: 0).CreateSchedule().WaveCount, Is.Null, "zero waves is endless");
        }

        [TestCase("startingCurrency")]
        [TestCase("killReward")]
        [TestCase("firstEnemyCount")]
        [TestCase("maxAlive")]
        [TestCase("waveCount")]
        [TestCase("interactRadius")]
        [TestCase("alertInterval")]
        public void ABadValueNamesItsArgument(string name)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => _ = Bad(name));

            Assert.That(e.ParamName, Is.EqualTo(name));
        }

        [Test]
        public void TheRulesFileChangesTheWavesAndTheCurrency()
        {
            var read = NetMatchFile.Parse(Text("{\"horde\": {\"waveCount\": 8, \"firstEnemyCount\": 5, \"startingCurrency\": 120, \"purchasesOnlyBetweenWaves\": false}}"), "horde.json")
                .Settings.Horde;

            Assert.That(read.WaveCount, Is.EqualTo(8));
            Assert.That(read.FirstEnemyCount, Is.EqualTo(5));
            Assert.That(read.StartingCurrency, Is.EqualTo(120));
            Assert.That(read.PurchasesOnlyBetweenWaves, Is.False);
            Assert.That(read.MaxAlive, Is.EqualTo(HordeSettings.DefaultMaxAlive), "an item left out keeps its default");
        }

        [Test]
        public void ARulesFileWithoutTheSectionUsesTheDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"scoreLimit\": 5}"), "old.json").Settings;

            Assert.That(read.Horde, Is.SameAs(HordeSettings.Default));
        }

        [TestCase("{\"horde\": {\"maxAlive\": 0}}", "match.horde.maxAlive")]
        [TestCase("{\"horde\": {\"waveCount\": -1}}", "match.horde.waveCount")]
        [TestCase("{\"horde\": {\"killReward\": -5}}", "match.horde.killReward")]
        [TestCase("{\"horde\": {\"restSeconds\": \"5\"}}", "match.horde.restSeconds")]
        [TestCase("{\"horde\": {\"enemies\": 3}}", "match.horde.enemies")]
        public void ABadValueNamesTheItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "bad.json"));

            Assert.That(e.Item, Is.EqualTo(item));
        }

        [Test]
        public void AHordeFileAlwaysListsTheSectionAndReadsItBack()
        {
            var settings = new NetMatchSettings(horde: new HordeSettings(waveCount: 7, restSeconds: 12.5f));
            var text = NetMatchFile.Write(File(settings, GameRules.Default.WithObjective(MatchObjective.Horde)));

            Assert.That(text, Does.Contain("\"horde\": {"));
            Assert.That(text, Does.Contain("\"waveCount\": 7"));
            Assert.That(text, Does.Contain("\"restSeconds\": 12.5"));
            Assert.That(NetMatchFile.Parse(text, "horde.json").Settings.Horde, Is.EqualTo(settings.Horde));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "horde.json")), Is.EqualTo(text));

            var defaults = NetMatchFile.Write(File(NetMatchSettings.Default, GameRules.Default.WithObjective(MatchObjective.Horde)));
            Assert.That(defaults, Does.Contain("\"horde\": {"), "the defaults are shown in a Horde file so they can be tuned");
        }

        [Test]
        public void OtherObjectivesLeaveTheDefaultSectionOutSoFilesWrittenBeforeItStayTheSame()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Not.Contain("\"horde\""));

            var changed = NetMatchFile.Write(File(new NetMatchSettings(horde: new HordeSettings(maxAlive: 6))));
            Assert.That(changed, Does.Contain("\"maxAlive\": 6"), "a changed value is never dropped");
        }

        [Test]
        public void WithPrivacyKeepsTheHordeSettings()
        {
            var horde = new HordeSettings(waveCount: 9);

            Assert.That(new NetMatchSettings(horde: horde).WithPrivacy(true, "ABC234").Horde, Is.SameAs(horde));
        }

        [Test]
        public void TheCommittedHordeRulesFileReadsItsObjectiveAndTheDefaults()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "Horde.json");

            var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

            Assert.That(file.Name, Is.EqualTo("Horde"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Horde));
            Assert.That(file.Rules.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(file.Settings.Horde, Is.EqualTo(HordeSettings.Default));
        }

        private static HordeSettings Bad(string name)
        {
            switch (name)
            {
                case "startingCurrency":
                    return new HordeSettings(startingCurrency: -1);
                case "killReward":
                    return new HordeSettings(killReward: -1);
                case "firstEnemyCount":
                    return new HordeSettings(firstEnemyCount: 0);
                case "maxAlive":
                    return new HordeSettings(maxAlive: 0);
                case "waveCount":
                    return new HordeSettings(waveCount: -1);
                case "interactRadius":
                    return new HordeSettings(interactRadius: 0f);
                case "alertInterval":
                    return new HordeSettings(alertInterval: float.NaN);
                default:
                    throw new ArgumentException(name);
            }
        }
    }
}

using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchInfectedSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings, MatchObjective objective = MatchObjective.None)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                objective.ToString(),
                GameRules.Default.WithObjective(objective),
                settings,
                "DefaultLoadout",
                new LoadoutRule(new[] { new[] { WeaponCategory.Handgun } }),
                new[] { handgun },
                0.4f,
                null,
                null);
        }

        private static string Text(string match, string objective = "Infected")
        {
            return "{\"rules\": {\"objective\": \"" + objective + "\"}, \"match\": " + match
                + ", \"loadout\": {\"slots\": [[\"Handgun\"]], \"weapons\": [\"" + _handgunId + "\"]},"
                + " \"weapons\": [{\"id\": \"" + _handgunId + "\", \"category\": \"Handgun\", \"fireMode\": \"Semi\", \"magazineSize\": 12,"
                + " \"fireInterval\": 0.2, \"reloadSeconds\": 1.5, \"damage\": 34, \"range\": 60}]}";
        }

        [Test]
        public void TheDefaultIsTheCoreDefault()
        {
            Assert.That(NetMatchSettings.Default.Infected, Is.SameAs(InfectedSpec.Default));
            Assert.That(new NetMatchSettings(infected: null).Infected, Is.SameAs(InfectedSpec.Default));
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(infected: new InfectedSpec(2, 15f, 240f, 200, 1.3f, 1.5f, 2, 5, " claws "));
            var text = NetMatchFile.Write(File(original, MatchObjective.Infected));

            var read = NetMatchFile.Parse(text, "Infected.json").Settings.Infected;

            Assert.That(read.InitialInfected, Is.EqualTo(2));
            Assert.That(read.StartDelaySeconds, Is.EqualTo(15f));
            Assert.That(read.RoundSeconds, Is.EqualTo(240f));
            Assert.That(read.InfectedMaxHealth, Is.EqualTo(200));
            Assert.That(read.InfectedSpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(read.InfectedRespawnSeconds, Is.EqualTo(1.5f));
            Assert.That(read.ScorePerInfection, Is.EqualTo(2));
            Assert.That(read.ScorePerSurvivedRound, Is.EqualTo(5));
            Assert.That(read.InfectedWeaponId, Is.EqualTo("claws"));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "Infected.json")), Is.EqualTo(text));
        }

        [Test]
        public void AnInfectedFileListsTheSectionEvenAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default, MatchObjective.Infected));

            Assert.That(text, Does.Contain(
                "\"infected\": {\n      \"initialInfected\": 1,\n      \"startDelaySeconds\": 10,\n      \"roundSeconds\": 300,\n"
                + "      \"infectedMaxHealth\": 150,\n      \"infectedSpeedMultiplier\": 1.15,\n      \"infectedRespawnSeconds\": 3,\n"
                + "      \"scorePerInfection\": 1,\n      \"scorePerSurvivedRound\": 3,\n      \"infectedWeaponId\": \"\"\n    }\n  },"));
        }

        // 節を足す前に書き出したルールのファイルを、書き出し直さずにそのまま使えるようにする
        [Test]
        public void AnotherObjectiveLeavesTheSectionOutAtTheDefaults()
        {
            var text = NetMatchFile.Write(File(NetMatchSettings.Default));

            Assert.That(text, Does.Not.Contain("\"infected\""));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Infected, Is.SameAs(InfectedSpec.Default));
        }

        // プレイリストは最初の項目のルールの match.* を使い続けるので、Infected でないルールにも変えた値は書く
        [Test]
        public void AnotherObjectiveWritesTheSectionWhenItWasChanged()
        {
            var text = NetMatchFile.Write(File(new NetMatchSettings(infected: new InfectedSpec(roundSeconds: 120f))));

            Assert.That(text, Does.Contain("\"infected\": {"));
            Assert.That(NetMatchFile.Parse(text, "TeamDeathmatch.json").Settings.Infected.RoundSeconds, Is.EqualTo(120f));
            Assert.That(NetMatchFile.Write(NetMatchFile.Parse(text, "TeamDeathmatch.json")), Is.EqualTo(text));
        }

        [Test]
        public void MissingItemsReadAsTheirDefaults()
        {
            var read = NetMatchFile.Parse(Text("{\"infected\": {\"initialInfected\": 3}}"), "Infected.json").Settings.Infected;

            Assert.That(read.InitialInfected, Is.EqualTo(3));
            Assert.That(read.StartDelaySeconds, Is.EqualTo(InfectedSpec.DefaultStartDelaySeconds));
            Assert.That(read.RoundSeconds, Is.EqualTo(InfectedSpec.DefaultRoundSeconds));
            Assert.That(read.InfectedRespawnSeconds, Is.EqualTo(InfectedSpec.DefaultInfectedRespawnSeconds));
            Assert.That(read.HasInfectedWeapon, Is.False);
            Assert.That(NetMatchFile.Parse(Text("{}"), "Infected.json").Settings.Infected, Is.SameAs(InfectedSpec.Default));
        }

        [TestCase("{\"infected\": {\"initialInfected\": 0}}", "match.infected.initialInfected")]
        [TestCase("{\"infected\": {\"startDelaySeconds\": -1}}", "match.infected.startDelaySeconds")]
        [TestCase("{\"infected\": {\"startDelaySeconds\": 300}}", "match.infected.startDelaySeconds")]
        [TestCase("{\"infected\": {\"roundSeconds\": 0}}", "match.infected.roundSeconds")]
        [TestCase("{\"infected\": {\"infectedMaxHealth\": 0}}", "match.infected.infectedMaxHealth")]
        [TestCase("{\"infected\": {\"infectedSpeedMultiplier\": 0}}", "match.infected.infectedSpeedMultiplier")]
        [TestCase("{\"infected\": {\"infectedRespawnSeconds\": -1}}", "match.infected.infectedRespawnSeconds")]
        [TestCase("{\"infected\": {\"scorePerInfection\": -1}}", "match.infected.scorePerInfection")]
        [TestCase("{\"infected\": {\"scorePerSurvivedRound\": -1}}", "match.infected.scorePerSurvivedRound")]
        [TestCase("{\"infected\": {\"infectedWeaponId\": 3}}", "match.infected.infectedWeaponId")]
        [TestCase("{\"infected\": {\"infectedCount\": 1}}", "match.infected.infectedCount")]
        [TestCase("{\"infected\": 1}", "match.infected")]
        public void ABadValueNamesItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "Infected.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("Infected.json"));
        }

        [Test]
        public void WithPrivacyKeepsTheInfectedSettings()
        {
            var original = new NetMatchSettings(infected: new InfectedSpec(initialInfected: 2));

            Assert.That(original.WithPrivacy(true, "ABC234").Infected, Is.SameAs(original.Infected));
        }

        [Test]
        public void TheCommittedInfectedRulesReadTheirObjectiveAndTuning()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "Infected.json");

            var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

            Assert.That(file.Name, Is.EqualTo("Infected"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Infected));
            Assert.That(file.Settings.Infected.InitialInfected, Is.EqualTo(InfectedSpec.DefaultInitialInfected));
            Assert.That(file.Settings.Infected.StartDelaySeconds, Is.EqualTo(InfectedSpec.DefaultStartDelaySeconds));
            Assert.That(file.Settings.Infected.RoundSeconds, Is.EqualTo(InfectedSpec.DefaultRoundSeconds));
            Assert.That(file.Settings.Infected.InfectedRespawnSeconds, Is.EqualTo(InfectedSpec.DefaultInfectedRespawnSeconds));
        }

        [Test]
        public void TheOtherCommittedRulesLeaveTheSectionOut()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);
                if (file.Rules.Objective == MatchObjective.Infected)
                {
                    continue;
                }

                Assert.That(System.IO.File.ReadAllText(path), Does.Not.Contain("\"" + NetMatchFile.InfectedItem + "\""), path);
                Assert.That(file.Settings.Infected, Is.SameAs(InfectedSpec.Default), path);
            }
        }
    }
}

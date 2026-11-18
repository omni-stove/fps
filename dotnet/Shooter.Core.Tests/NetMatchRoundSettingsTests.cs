using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class NetMatchRoundSettingsTests
    {
        private const string _handgunId = "b4e2a9d7-3c16-4a58-8f2e-7d0b9c6a1e45";

        private static NetMatchFile File(NetMatchSettings settings)
        {
            var handgun = new WeaponSpec(
                _handgunId, "weapon.handgun", WeaponCategory.Handgun, FireMode.Semi, 12, 48, 0.2f, 1.5f, 34, 60f,
                DamageFalloff.None);
            return new NetMatchFile(
                "Elimination",
                GameRules.Default.WithObjective(MatchObjective.Elimination),
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
        public void DefaultsPlayThreeWinningRoundsOfTwoMinutesWithFiveSecondsBetween()
        {
            var settings = NetMatchSettings.Default;

            Assert.That(settings.RoundWinsToWin, Is.EqualTo(3));
            Assert.That(settings.RoundPreparationSeconds, Is.EqualTo(5f));
            Assert.That(settings.RoundTimeLimitSeconds, Is.EqualTo(120f));
        }

        [Test]
        public void WrittenValuesReadBack()
        {
            var original = new NetMatchSettings(roundWinsToWin: 5, roundPreparationSeconds: 2.5f, roundTimeLimitSeconds: 90f);

            var read = NetMatchFile.Parse(NetMatchFile.Write(File(original)), "Elimination.json");

            Assert.That(read.Rules.Objective, Is.EqualTo(MatchObjective.Elimination));
            Assert.That(read.Settings.RoundWinsToWin, Is.EqualTo(5));
            Assert.That(read.Settings.RoundPreparationSeconds, Is.EqualTo(2.5f));
            Assert.That(read.Settings.RoundTimeLimitSeconds, Is.EqualTo(90f));
        }

        [Test]
        public void MissingItemsReadAsTheDefaults()
        {
            var settings = NetMatchFile.Parse(Text("{}"), "Elimination.json").Settings;

            Assert.That(settings.RoundWinsToWin, Is.EqualTo(NetMatchSettings.DefaultRoundWinsToWin));
            Assert.That(settings.RoundPreparationSeconds, Is.EqualTo(NetMatchSettings.DefaultRoundPreparationSeconds));
            Assert.That(settings.RoundTimeLimitSeconds, Is.EqualTo(NetMatchSettings.DefaultRoundTimeLimitSeconds));
        }

        [Test]
        public void WithPrivacyKeepsTheRoundSettings()
        {
            var settings = new NetMatchSettings(roundWinsToWin: 4, roundPreparationSeconds: 0f, roundTimeLimitSeconds: 60f).WithPrivacy(true, "abc");

            Assert.That(settings.RoundWinsToWin, Is.EqualTo(4));
            Assert.That(settings.RoundPreparationSeconds, Is.Zero);
            Assert.That(settings.RoundTimeLimitSeconds, Is.EqualTo(60f));
        }

        [TestCase("{\"roundWinsToWin\": 0}", "roundWinsToWin")]
        [TestCase("{\"roundPreparationSeconds\": -1}", "roundPreparationSeconds")]
        [TestCase("{\"roundTimeLimitSeconds\": 0}", "roundTimeLimitSeconds")]
        public void RejectsAValueOutOfRangeAtItsItem(string match, string item)
        {
            var e = Assert.Throws<ServerDataJsonException>(() => NetMatchFile.Parse(Text(match), "Elimination.json"));

            Assert.That(e.Message, Does.Contain(item));
        }

        [Test]
        public void TheCommittedEliminationRulesPlayRounds()
        {
            var path = Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules", "Elimination.json");

            var file = NetMatchFile.Parse(System.IO.File.ReadAllText(path), path);

            Assert.That(file.Name, Is.EqualTo("Elimination"));
            Assert.That(file.Rules.Objective, Is.EqualTo(MatchObjective.Elimination));
            Assert.That(file.Rules.RespawnMode, Is.EqualTo(RespawnMode.None));
            Assert.That(file.Settings.RoundWinsToWin, Is.EqualTo(3));
            Assert.That(file.Settings.RoundPreparationSeconds, Is.EqualTo(5f));
            Assert.That(file.Settings.RoundTimeLimitSeconds, Is.EqualTo(120f));
        }

        [Test]
        public void EveryCommittedRulesFileListsTheRoundItems()
        {
            foreach (var path in Directory.GetFiles(Path.Combine(NetMatchFileTests.ServerDataRoot(), "rules"), "*" + NetMatchFile.Extension))
            {
                var text = System.IO.File.ReadAllText(path);
                foreach (var item in new[] { NetMatchFile.RoundWinsToWinItem, NetMatchFile.RoundPreparationSecondsItem, NetMatchFile.RoundTimeLimitSecondsItem })
                {
                    Assert.That(text, Does.Contain("\"" + item + "\": "), path + " " + item);
                }
            }
        }
    }
}

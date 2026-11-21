using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class BotTacticsSpecTests
    {
        [Test]
        public void TheDefaultsLetBotsThrowSwingAndHide()
        {
            var tactics = NetBotSpec.Default.Tactics;

            Assert.That(tactics, Is.SameAs(BotTacticsSpec.Default));
            Assert.That(tactics.ThrowGrenades, Is.True);
            Assert.That(tactics.GrenadeMinDistance, Is.EqualTo(8f));
            Assert.That(tactics.GrenadeMaxDistance, Is.EqualTo(20f));
            Assert.That(tactics.GrenadeIntervalSeconds, Is.EqualTo(10f));
            Assert.That(tactics.MeleeAttacks, Is.True);
            Assert.That(tactics.TakeCover, Is.True);
            Assert.That(tactics.CoverSearchRadius, Is.EqualTo(8f));
            Assert.That(tactics.CoverSearchStep, Is.EqualTo(2f));
            Assert.That(tactics.CoverSearchDirections, Is.EqualTo(8));
            Assert.That(tactics.CoverSearchSeconds, Is.EqualTo(1f));
            Assert.That(tactics.CoverMaxAngleDegrees, Is.EqualTo(CoverPointSelector.DefaultMaxCoverAngleDegrees));
            Assert.That(tactics.CoverMinThreatDistance, Is.EqualTo(CoverPointSelector.DefaultMinThreatDistance));
        }

        [Test]
        public void ReadsEveryTacticsItem()
        {
            var tactics = TuningJson.ParseBots(
                "{\"throwGrenades\": false, \"grenadeMinDistance\": 6, \"grenadeMaxDistance\": 15, \"grenadeIntervalSeconds\": 4,"
                + " \"meleeAttacks\": false, \"takeCover\": false, \"coverSearchRadius\": 10, \"coverSearchStep\": 2.5,"
                + " \"coverSearchDirections\": 12, \"coverSearchSeconds\": 0.5, \"coverMaxAngleDegrees\": 45, \"coverMinThreatDistance\": 3}",
                "bots.json").Tactics;

            Assert.That(tactics.ThrowGrenades, Is.False);
            Assert.That(tactics.GrenadeMinDistance, Is.EqualTo(6f));
            Assert.That(tactics.GrenadeMaxDistance, Is.EqualTo(15f));
            Assert.That(tactics.GrenadeIntervalSeconds, Is.EqualTo(4f));
            Assert.That(tactics.MeleeAttacks, Is.False);
            Assert.That(tactics.TakeCover, Is.False);
            Assert.That(tactics.CoverSearchRadius, Is.EqualTo(10f));
            Assert.That(tactics.CoverSearchStep, Is.EqualTo(2.5f));
            Assert.That(tactics.CoverSearchDirections, Is.EqualTo(12));
            Assert.That(tactics.CoverSearchSeconds, Is.EqualTo(0.5f));
            Assert.That(tactics.CoverMaxAngleDegrees, Is.EqualTo(45f));
            Assert.That(tactics.CoverMinThreatDistance, Is.EqualTo(3f));
        }

        [Test]
        public void LeftOutTacticsItemsKeepTheDefaults()
        {
            var tactics = TuningJson.ParseBots("{\"sightRange\": 30}", "bots.json").Tactics;

            Assert.That(tactics.ThrowGrenades, Is.EqualTo(BotTacticsSpec.DefaultThrowGrenades));
            Assert.That(tactics.GrenadeMaxDistance, Is.EqualTo(BotTacticsSpec.DefaultGrenadeMaxDistance));
            Assert.That(tactics.CoverSearchDirections, Is.EqualTo(BotTacticsSpec.DefaultCoverSearchDirections));
        }

        [Test]
        public void ThePackagedBotsKeepTheDefaultTactics()
        {
            var text = System.IO.File.ReadAllText(System.IO.Path.Combine(RepositoryRoot(), "server-data", "tuning", "bots.json"));
            var tactics = TuningJson.ParseBots(text, "bots.json").Tactics;

            Assert.That(tactics.ThrowGrenades, Is.EqualTo(BotTacticsSpec.Default.ThrowGrenades));
            Assert.That(tactics.MeleeAttacks, Is.EqualTo(BotTacticsSpec.Default.MeleeAttacks));
            Assert.That(tactics.TakeCover, Is.EqualTo(BotTacticsSpec.Default.TakeCover));
        }

        [TestCase("{\"grenadeMinDistance\": -1}", "grenadeMinDistance")]
        [TestCase("{\"grenadeMinDistance\": 10, \"grenadeMaxDistance\": 5}", "grenadeMaxDistance")]
        [TestCase("{\"grenadeIntervalSeconds\": -1}", "grenadeIntervalSeconds")]
        [TestCase("{\"coverSearchRadius\": 0}", "coverSearchRadius")]
        [TestCase("{\"coverSearchStep\": 0}", "coverSearchStep")]
        [TestCase("{\"coverSearchRadius\": 100, \"coverSearchStep\": 1}", "coverSearchStep")]
        [TestCase("{\"coverSearchDirections\": 0}", "coverSearchDirections")]
        [TestCase("{\"coverSearchDirections\": 65}", "coverSearchDirections")]
        [TestCase("{\"coverSearchSeconds\": -1}", "coverSearchSeconds")]
        [TestCase("{\"coverMaxAngleDegrees\": 181}", "coverMaxAngleDegrees")]
        [TestCase("{\"coverMinThreatDistance\": -1}", "coverMinThreatDistance")]
        [TestCase("{\"takeCover\": 1}", "takeCover")]
        [TestCase("{\"throwGrenades\": \"yes\"}", "throwGrenades")]
        public void ABadTacticsItemIsNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseBots(json, "bots.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bots.json").And.Contain("'" + item + "'"));
        }

        [Test]
        public void TheSpecRejectsValuesOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotTacticsSpec(grenadeMinDistance: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotTacticsSpec(grenadeMinDistance: 5f, grenadeMaxDistance: 4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotTacticsSpec(coverSearchRadius: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new BotTacticsSpec(coverSearchDirections: BotTacticsSpec.MaxCoverSearchDirections + 1));
            Assert.DoesNotThrow(() => new BotTacticsSpec(coverSearchRadius: 32f, coverSearchStep: 1f));
            Assert.DoesNotThrow(() => new BotTacticsSpec(grenadeMinDistance: 5f, grenadeMaxDistance: 5f));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new System.IO.DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (System.IO.Directory.Exists(System.IO.Path.Combine(directory.FullName, "server-data")))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}

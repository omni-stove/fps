using System.IO;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class BotsBrainTuningJsonTests
    {
        [Test]
        public void ReadsEveryEnemySpecItem()
        {
            var spec = TuningJson.ParseBots(
                "{\"openingSpreadDegrees\": 4, \"settledSpreadDegrees\": 1, \"openingShots\": 5,"
                + " \"coverHideSeconds\": 2.5, \"coverPeekSeconds\": 3, \"coverHealthFraction\": 0.25,"
                + " \"archetype\": \"Sniper\", \"staggerResistance\": 0.5, \"fireRange\": 35, \"facingToleranceDegrees\": 20,"
                + " \"rangeSpreadDegrees\": 1.5, \"sprintRatio\": 1.4, \"crouchRatio\": 0.6, \"recoilControl\": 0.9}",
                "bots.json");
            var brain = spec.Brain;

            Assert.That(brain.OpeningSpreadDegrees, Is.EqualTo(4f));
            Assert.That(brain.SettledSpreadDegrees, Is.EqualTo(1f));
            Assert.That(brain.OpeningShots, Is.EqualTo(5));
            Assert.That(brain.CoverHideSeconds, Is.EqualTo(2.5f));
            Assert.That(brain.CoverPeekSeconds, Is.EqualTo(3f));
            Assert.That(brain.CoverHealthFraction, Is.EqualTo(0.25f));
            Assert.That(brain.Archetype, Is.EqualTo(EnemyArchetype.Sniper));
            Assert.That(brain.StaggerResistance, Is.EqualTo(0.5f));
            Assert.That(brain.FireRange, Is.EqualTo(35f));
            Assert.That(brain.FireToleranceDegrees, Is.EqualTo(20f));
            Assert.That(brain.RangeSpreadDegrees, Is.EqualTo(1.5f));
            Assert.That(brain.SprintRatio, Is.EqualTo(1.4f));
            Assert.That(brain.CrouchRatio, Is.EqualTo(0.6f));
            Assert.That(brain.RecoilControl, Is.EqualTo(0.9f));
            Assert.That(spec.Input.FireToleranceDegrees, Is.EqualTo(BotInputSpec.DefaultFireToleranceDegrees));
        }

        [Test]
        public void TheAimToleranceAndTheFacingToleranceAreSeparateItems()
        {
            var spec = TuningJson.ParseBots("{\"fireToleranceDegrees\": 1}", "bots.json");

            Assert.That(spec.Input.FireToleranceDegrees, Is.EqualTo(1f));
            Assert.That(spec.Brain.FireToleranceDegrees, Is.EqualTo(EnemySpec.MaxFireToleranceDegrees));
        }

        [Test]
        public void TheArrivalDistanceSetsWhereTheDriverStopsAndWhereTheBrainArrives()
        {
            var spec = TuningJson.ParseBots("{\"arrivalDistance\": 1.25}", "bots.json");

            Assert.That(spec.Input.ArrivalDistance, Is.EqualTo(1.25f));
            Assert.That(spec.Brain.ArrivalDistance, Is.EqualTo(1.25f));
        }

        [Test]
        public void LeftOutItemsKeepTheBotDefaults()
        {
            var brain = TuningJson.ParseBots("{}", "bots.json").Brain;

            Assert.That(brain.FireRange, Is.EqualTo(25f));
            Assert.That(brain.FireToleranceDegrees, Is.EqualTo(180f));
            Assert.That(brain.RangeSpreadDegrees, Is.EqualTo(0f));
            Assert.That(brain.OpeningSpreadDegrees, Is.EqualTo(0f));
            Assert.That(brain.SettledSpreadDegrees, Is.EqualTo(0f));
            Assert.That(brain.OpeningShots, Is.EqualTo(0));
            Assert.That(brain.CoverHideSeconds, Is.EqualTo(1.5f));
            Assert.That(brain.CoverPeekSeconds, Is.EqualTo(2f));
            Assert.That(brain.CoverHealthFraction, Is.EqualTo(0.5f));
            Assert.That(brain.Archetype, Is.EqualTo(EnemyArchetype.Rifleman));
            Assert.That(brain.StaggerResistance, Is.EqualTo(0f));
            Assert.That(brain.ArrivalDistance, Is.EqualTo(0.5f));
            Assert.That(brain.SprintRatio, Is.EqualTo(1.6f));
            Assert.That(brain.CrouchRatio, Is.EqualTo(0.5f));
            Assert.That(brain.RecoilControl, Is.EqualTo(0.75f));
        }

        [Test]
        public void WithoutAStaggerResistanceTheArchetypeDefaultIsUsed()
        {
            var brain = TuningJson.ParseBots("{\"archetype\": \"Shield\"}", "bots.json").Brain;

            Assert.That(brain.StaggerResistance, Is.EqualTo(1.5f));
        }

        [Test]
        public void WithoutAFireRangeItFollowsTheAttackRange()
        {
            var spec = TuningJson.ParseBots("{\"attackRange\": 10}", "bots.json");

            Assert.That(spec.Brain.FireRange, Is.EqualTo(10f));
        }

        [Test]
        public void TheFireRangeIsKeptWithinTheSightRange()
        {
            var spec = TuningJson.ParseBots("{\"sightRange\": 30, \"fireRange\": 50}", "bots.json");

            Assert.That(spec.Brain.FireRange, Is.EqualTo(30f));
        }

        [Test]
        public void ThePackagedBotsAreTheDefaults()
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "Assets", "game", "Resources", "Tuning", "bots.json"));
            var spec = TuningJson.ParseBots(text, "bots.json");
            var expected = NetBotSpec.Default;

            Assert.That(spec.Brain.SightRange, Is.EqualTo(expected.Brain.SightRange));
            Assert.That(spec.Brain.AttackRange, Is.EqualTo(expected.Brain.AttackRange));
            Assert.That(spec.Brain.LoseSightSeconds, Is.EqualTo(expected.Brain.LoseSightSeconds));
            Assert.That(spec.Brain.SearchSeconds, Is.EqualTo(expected.Brain.SearchSeconds));
            Assert.That(spec.Brain.ReactionSeconds, Is.EqualTo(expected.Brain.ReactionSeconds));
            Assert.That(spec.Brain.OpeningSpreadDegrees, Is.EqualTo(expected.Brain.OpeningSpreadDegrees));
            Assert.That(spec.Brain.SettledSpreadDegrees, Is.EqualTo(expected.Brain.SettledSpreadDegrees));
            Assert.That(spec.Brain.OpeningShots, Is.EqualTo(expected.Brain.OpeningShots));
            Assert.That(spec.Brain.CoverHideSeconds, Is.EqualTo(expected.Brain.CoverHideSeconds));
            Assert.That(spec.Brain.CoverPeekSeconds, Is.EqualTo(expected.Brain.CoverPeekSeconds));
            Assert.That(spec.Brain.CoverHealthFraction, Is.EqualTo(expected.Brain.CoverHealthFraction));
            Assert.That(spec.Brain.Archetype, Is.EqualTo(expected.Brain.Archetype));
            Assert.That(spec.Brain.StaggerResistance, Is.EqualTo(expected.Brain.StaggerResistance));
            Assert.That(spec.Brain.FireRange, Is.EqualTo(expected.Brain.FireRange));
            Assert.That(spec.Brain.FireToleranceDegrees, Is.EqualTo(expected.Brain.FireToleranceDegrees));
            Assert.That(spec.Brain.RangeSpreadDegrees, Is.EqualTo(expected.Brain.RangeSpreadDegrees));
            Assert.That(spec.Brain.ArrivalDistance, Is.EqualTo(expected.Brain.ArrivalDistance));
            Assert.That(spec.Brain.SprintRatio, Is.EqualTo(expected.Brain.SprintRatio));
            Assert.That(spec.Brain.CrouchRatio, Is.EqualTo(expected.Brain.CrouchRatio));
            Assert.That(spec.Brain.RecoilControl, Is.EqualTo(expected.Brain.RecoilControl));
            Assert.That(spec.Input.MaxTurnDegreesPerSecond, Is.EqualTo(expected.Input.MaxTurnDegreesPerSecond));
            Assert.That(spec.Input.AimErrorPerMeter, Is.EqualTo(expected.Input.AimErrorPerMeter));
            Assert.That(spec.Input.AimErrorRefreshSeconds, Is.EqualTo(expected.Input.AimErrorRefreshSeconds));
            Assert.That(spec.Input.FireToleranceDegrees, Is.EqualTo(expected.Input.FireToleranceDegrees));
            Assert.That(spec.Input.ArrivalDistance, Is.EqualTo(expected.Input.ArrivalDistance));
            Assert.That(spec.Input.SprintDistance, Is.EqualTo(expected.Input.SprintDistance));
            Assert.That(spec.Input.SprintMaxAngleDegrees, Is.EqualTo(expected.Input.SprintMaxAngleDegrees));
            Assert.That(spec.Input.CrouchWhenHolding, Is.EqualTo(expected.Input.CrouchWhenHolding));
            Assert.That(spec.NavSampleRadius, Is.EqualTo(expected.NavSampleRadius));
            Assert.That(spec.RepathSeconds, Is.EqualTo(expected.RepathSeconds));
            Assert.That(spec.RepathDistance, Is.EqualTo(expected.RepathDistance));
            Assert.That(spec.StuckSeconds, Is.EqualTo(expected.StuckSeconds));
            Assert.That(spec.StuckProgress, Is.EqualTo(expected.StuckProgress));
        }

        [TestCase("{\"facingToleranceDegrees\": 181}", "facingToleranceDegrees")]
        [TestCase("{\"fireToleranceDegrees\": 0}", "fireToleranceDegrees")]
        [TestCase("{\"fireRange\": -1}", "fireRange")]
        [TestCase("{\"openingSpreadDegrees\": -1}", "openingSpreadDegrees")]
        [TestCase("{\"openingShots\": -1}", "openingShots")]
        [TestCase("{\"openingShots\": 1.5}", "openingShots")]
        [TestCase("{\"coverHealthFraction\": 2}", "coverHealthFraction")]
        [TestCase("{\"staggerResistance\": -1}", "staggerResistance")]
        [TestCase("{\"staggerResistance\": null}", "staggerResistance")]
        [TestCase("{\"arrivalDistance\": 0}", "arrivalDistance")]
        [TestCase("{\"recoilControl\": 1.5}", "recoilControl")]
        [TestCase("{\"archetype\": \"Tank\"}", "archetype")]
        [TestCase("{\"archetype\": \"1\"}", "archetype")]
        [TestCase("{\"archetype\": 1}", "archetype")]
        public void ABadBrainItemIsNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseBots(json, "bots.json"));

            Assert.That(e.Item, Is.EqualTo(item));
            Assert.That(e.Message, Does.Contain("bots.json").And.Contain("'" + item + "'"));
        }

        private static string RepositoryRoot()
        {
            for (var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory); directory != null; directory = directory.Parent)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "server-data")))
                {
                    return directory.FullName;
                }
            }

            Assert.Fail("server-data is not above " + TestContext.CurrentContext.TestDirectory);
            return null;
        }
    }
}

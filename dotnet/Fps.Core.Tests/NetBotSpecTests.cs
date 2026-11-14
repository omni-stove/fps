using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NetBotSpecTests
    {
        [Test]
        public void TheDefaultsAreTheUnityServerBotValues()
        {
            var spec = NetBotSpec.Default;

            Assert.That(spec.Brain.SightRange, Is.EqualTo(40f));
            Assert.That(spec.Brain.AttackRange, Is.EqualTo(25f));
            Assert.That(spec.Brain.LoseSightSeconds, Is.EqualTo(2f));
            Assert.That(spec.Brain.SearchSeconds, Is.EqualTo(6f));
            Assert.That(spec.Brain.ReactionSeconds, Is.EqualTo(0.6f));
            Assert.That(spec.Input, Is.SameAs(BotInputSpec.Default));
            Assert.That(spec.NavSampleRadius, Is.EqualTo(2.5f));
            Assert.That(spec.RepathSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.RepathDistance, Is.EqualTo(1f));
            Assert.That(spec.StuckSeconds, Is.EqualTo(2f));
            Assert.That(spec.StuckProgress, Is.EqualTo(0.5f));
        }

        [Test]
        public void ReadsEveryBotItem()
        {
            var spec = TuningJson.ParseBots(
                "{\"sightRange\": 30, \"attackRange\": 20, \"loseSightSeconds\": 1, \"searchSeconds\": 4, \"reactionSeconds\": 0.3,"
                + " \"maxTurnDegreesPerSecond\": 180, \"aimErrorPerMeter\": 0.05, \"aimErrorRefreshSeconds\": 0.25, \"fireToleranceDegrees\": 5,"
                + " \"arrivalDistance\": 0.75, \"sprintDistance\": 10, \"sprintMaxAngleDegrees\": 20, \"crouchWhenHolding\": true,"
                + " \"navSampleRadius\": 3, \"repathSeconds\": 1, \"repathDistance\": 2, \"stuckSeconds\": 3, \"stuckProgress\": 0.25}",
                "bots.json");

            Assert.That(spec.Brain.SightRange, Is.EqualTo(30f));
            Assert.That(spec.Brain.AttackRange, Is.EqualTo(20f));
            Assert.That(spec.Brain.LoseSightSeconds, Is.EqualTo(1f));
            Assert.That(spec.Brain.SearchSeconds, Is.EqualTo(4f));
            Assert.That(spec.Brain.ReactionSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.Input.MaxTurnDegreesPerSecond, Is.EqualTo(180f));
            Assert.That(spec.Input.AimErrorPerMeter, Is.EqualTo(0.05f));
            Assert.That(spec.Input.AimErrorRefreshSeconds, Is.EqualTo(0.25f));
            Assert.That(spec.Input.FireToleranceDegrees, Is.EqualTo(5f));
            Assert.That(spec.Input.ArrivalDistance, Is.EqualTo(0.75f));
            Assert.That(spec.Input.SprintDistance, Is.EqualTo(10f));
            Assert.That(spec.Input.SprintMaxAngleDegrees, Is.EqualTo(20f));
            Assert.That(spec.Input.CrouchWhenHolding, Is.True);
            Assert.That(spec.NavSampleRadius, Is.EqualTo(3f));
            Assert.That(spec.RepathSeconds, Is.EqualTo(1f));
            Assert.That(spec.RepathDistance, Is.EqualTo(2f));
            Assert.That(spec.StuckSeconds, Is.EqualTo(3f));
            Assert.That(spec.StuckProgress, Is.EqualTo(0.25f));
        }

        [Test]
        public void LeftOutItemsKeepTheDefaults()
        {
            var spec = TuningJson.ParseBots("{}", "bots.json");

            Assert.That(spec.Brain.SightRange, Is.EqualTo(NetBotSpec.DefaultSightRange));
            Assert.That(spec.Input.MaxTurnDegreesPerSecond, Is.EqualTo(BotInputSpec.DefaultMaxTurnDegreesPerSecond));
            Assert.That(spec.StuckSeconds, Is.EqualTo(NetBotSpec.DefaultStuckSeconds));
        }

        [TestCase("{\"attackRange\": 50}", "attackRange")]
        [TestCase("{\"maxTurnDegreesPerSecond\": 0}", "maxTurnDegreesPerSecond")]
        [TestCase("{\"stuckSeconds\": -1}", "stuckSeconds")]
        [TestCase("{\"crouchWhenHolding\": 1}", "crouchWhenHolding")]
        [TestCase("{\"sightRadius\": 10}", "sightRadius")]
        public void ABadItemIsNamed(string json, string item)
        {
            var e = Assert.Throws<TuningJsonException>(() => TuningJson.ParseBots(json, "bots.json"));

            Assert.That(e.Message, Does.Contain("bots.json").And.Contain(item));
        }

        [Test]
        public void RejectsMissingParts()
        {
            var defaults = NetBotSpec.Default;
            Assert.Throws<ArgumentNullException>(() => new NetBotSpec(null, defaults.Input, 1f, 1f, 1f, 1f, 1f));
            Assert.Throws<ArgumentNullException>(() => new NetBotSpec(defaults.Brain, null, 1f, 1f, 1f, 1f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetBotSpec(defaults.Brain, defaults.Input, 0f, 1f, 1f, 1f, 1f));
        }
    }
}

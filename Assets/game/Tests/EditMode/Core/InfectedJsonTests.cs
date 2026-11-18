using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class InfectedJsonTests
    {
        private const string _source = "infected.json";

        private static readonly string[] _expectedItemNames =
        {
            "initialInfected", "startDelaySeconds", "roundSeconds", "infectedMaxHealth",
            "infectedSpeedMultiplier", "infectedRespawnSeconds", "scorePerInfection", "scorePerSurvivedRound",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = InfectedJson.Parse(
                "{\"initialInfected\": 2, \"startDelaySeconds\": 5, \"roundSeconds\": 180, \"infectedMaxHealth\": 200,"
                + " \"infectedSpeedMultiplier\": 1.3, \"infectedRespawnSeconds\": 0, \"scorePerInfection\": 2, \"scorePerSurvivedRound\": 0}",
                _source);

            Assert.That(spec.InitialInfected, Is.EqualTo(2));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(5f));
            Assert.That(spec.RoundSeconds, Is.EqualTo(180f));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(200));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(0f));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(2));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(0));
        }

        [Test]
        public void LeftOutItemsUseTheDefaults()
        {
            var spec = InfectedJson.Parse("{}", _source);

            Assert.That(spec.InitialInfected, Is.EqualTo(1));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(10f));
            Assert.That(spec.RoundSeconds, Is.EqualTo(300f));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(150));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(1.15f));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(3f));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(1));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(3));
        }

        [Test]
        public void TheItemNamesAreTheConstructorArguments()
        {
            Assert.That(InfectedJson.ItemNames, Is.EqualTo(_expectedItemNames));
            Assert.That(InfectedJson.ResourcePath, Is.EqualTo("Tuning/infected"));
        }

        [Test]
        public void ABadValueNamesItsItem()
        {
            var initial = Reject("{\"initialInfected\": 0}");
            var lateStart = Reject("{\"startDelaySeconds\": 60, \"roundSeconds\": 60}");
            var round = Reject("{\"roundSeconds\": 0}");
            var health = Reject("{\"infectedMaxHealth\": 0}");
            var speed = Reject("{\"infectedSpeedMultiplier\": 0}");
            var respawn = Reject("{\"infectedRespawnSeconds\": -1}");
            var score = Reject("{\"scorePerInfection\": -1}");
            var fraction = Reject("{\"initialInfected\": 1.5}");

            Assert.That(initial.Item, Is.EqualTo("initialInfected"));
            Assert.That(initial.InnerException, Is.InstanceOf<ArgumentOutOfRangeException>());
            Assert.That(lateStart.Item, Is.EqualTo("startDelaySeconds"));
            Assert.That(lateStart.Message, Does.Contain("shorter than roundSeconds (60)"));
            Assert.That(round.Item, Is.EqualTo("roundSeconds"));
            Assert.That(health.Item, Is.EqualTo("infectedMaxHealth"));
            Assert.That(speed.Item, Is.EqualTo("infectedSpeedMultiplier"));
            Assert.That(respawn.Item, Is.EqualTo("infectedRespawnSeconds"));
            Assert.That(score.Item, Is.EqualTo("scorePerInfection"));
            Assert.That(fraction.Item, Is.EqualTo("initialInfected"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => InfectedJson.Parse(json, _source));
        }
    }
}

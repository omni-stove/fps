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
            "infectedSpeedMultiplier", "infectedRespawnSeconds", "scorePerInfection", "scorePerSurvivedRound", "infectedWeaponId",
        };

        [Test]
        public void ReadsEveryItem()
        {
            var spec = InfectedJson.Parse(
                "{\"initialInfected\": 2, \"startDelaySeconds\": 5, \"roundSeconds\": 180, \"infectedMaxHealth\": 200,"
                + " \"infectedSpeedMultiplier\": 1.3, \"infectedRespawnSeconds\": 0, \"scorePerInfection\": 2, \"scorePerSurvivedRound\": 0,"
                + " \"infectedWeaponId\": \" knife-01 \"}",
                _source);

            Assert.That(spec.InitialInfected, Is.EqualTo(2));
            Assert.That(spec.StartDelaySeconds, Is.EqualTo(5f));
            Assert.That(spec.RoundSeconds, Is.EqualTo(180f));
            Assert.That(spec.InfectedMaxHealth, Is.EqualTo(200));
            Assert.That(spec.InfectedSpeedMultiplier, Is.EqualTo(1.3f));
            Assert.That(spec.InfectedRespawnSeconds, Is.EqualTo(0f));
            Assert.That(spec.ScorePerInfection, Is.EqualTo(2));
            Assert.That(spec.ScorePerSurvivedRound, Is.EqualTo(0));
            Assert.That(spec.InfectedWeaponId, Is.EqualTo("knife-01"), "the id is trimmed");
            Assert.That(spec.HasInfectedWeapon, Is.True);
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
            Assert.That(spec.InfectedWeaponId, Is.Empty, "the infected keep the weapons they had");
            Assert.That(spec.HasInfectedWeapon, Is.False);
        }

        [Test]
        public void ABlankWeaponIdMeansTheInfectedKeepTheirWeapons()
        {
            Assert.That(new InfectedSpec(infectedWeaponId: null).InfectedWeaponId, Is.Empty);
            Assert.That(new InfectedSpec(infectedWeaponId: "  ").HasInfectedWeapon, Is.False);
            Assert.That(InfectedSpec.Default.HasInfectedWeapon, Is.False);
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
            var weapon = Reject("{\"infectedWeaponId\": 3}");

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
            Assert.That(weapon.Item, Is.EqualTo("infectedWeaponId"));
        }

        private static TuningJsonException Reject(string json)
        {
            return Assert.Throws<TuningJsonException>(() => InfectedJson.Parse(json, _source));
        }
    }
}

using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GameRulesTests
    {
        private static GameRules Rules(
            FriendlyFire friendlyFire = FriendlyFire.Off,
            RespawnMode respawnMode = RespawnMode.Individual,
            float waveIntervalSeconds = 10f,
            float regenDelaySeconds = 0f,
            float regenPerSecond = 0f,
            DeathBody deathBody = DeathBody.Vanish,
            DeathCamera deathCamera = DeathCamera.None,
            bool infiniteReserve = false,
            float reserveAmmoMultiplier = 1f)
        {
            return new GameRules(
                friendlyFire,
                respawnMode,
                waveIntervalSeconds,
                regenDelaySeconds,
                regenPerSecond,
                deathBody,
                deathCamera,
                infiniteReserve,
                reserveAmmoMultiplier);
        }

        [Test]
        public void DefaultMatchesTheCurrentBehaviour()
        {
            var rules = GameRules.Default;

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Off));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Individual));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(GameRules.DefaultWaveIntervalSeconds));
            Assert.That(rules.WaveIntervalSeconds, Is.GreaterThan(0f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(0f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Vanish));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.None));
            Assert.That(rules.InfiniteReserve, Is.False);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(1f));
        }

        [Test]
        public void DefaultIsTheSameInstanceEveryTime()
        {
            Assert.That(GameRules.Default, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void KeepsEveryGivenValue()
        {
            var rules = new GameRules(
                FriendlyFire.Reflect,
                RespawnMode.Wave,
                15f,
                4f,
                12.5f,
                DeathBody.Collapse,
                DeathCamera.Killer,
                true,
                2.5f);

            Assert.That(rules.FriendlyFire, Is.EqualTo(FriendlyFire.Reflect));
            Assert.That(rules.RespawnMode, Is.EqualTo(RespawnMode.Wave));
            Assert.That(rules.WaveIntervalSeconds, Is.EqualTo(15f));
            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(4f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(12.5f));
            Assert.That(rules.DeathBody, Is.EqualTo(DeathBody.Collapse));
            Assert.That(rules.DeathCamera, Is.EqualTo(DeathCamera.Killer));
            Assert.That(rules.InfiniteReserve, Is.True);
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(2.5f));
        }

        [Test]
        public void AcceptsEveryDefinedEnumValue()
        {
            foreach (FriendlyFire value in Enum.GetValues(typeof(FriendlyFire)))
            {
                Assert.That(Rules(friendlyFire: value).FriendlyFire, Is.EqualTo(value));
            }

            foreach (RespawnMode value in Enum.GetValues(typeof(RespawnMode)))
            {
                Assert.That(Rules(respawnMode: value).RespawnMode, Is.EqualTo(value));
            }

            foreach (DeathBody value in Enum.GetValues(typeof(DeathBody)))
            {
                Assert.That(Rules(deathBody: value).DeathBody, Is.EqualTo(value));
            }

            foreach (DeathCamera value in Enum.GetValues(typeof(DeathCamera)))
            {
                Assert.That(Rules(deathCamera: value).DeathCamera, Is.EqualTo(value));
            }
        }

        [Test]
        public void AcceptsZeroForTheNonNegativeValues()
        {
            var rules = Rules(regenDelaySeconds: 0f, regenPerSecond: 0f, reserveAmmoMultiplier: 0f);

            Assert.That(rules.RegenDelaySeconds, Is.EqualTo(0f));
            Assert.That(rules.RegenPerSecond, Is.EqualTo(0f));
            Assert.That(rules.ReserveAmmoMultiplier, Is.EqualTo(0f));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void RejectsAnUndefinedFriendlyFire(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(friendlyFire: (FriendlyFire)value));
            Assert.That(ex.ParamName, Is.EqualTo("friendlyFire"));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void RejectsAnUndefinedRespawnMode(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(respawnMode: (RespawnMode)value));
            Assert.That(ex.ParamName, Is.EqualTo("respawnMode"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectsAnUndefinedDeathBody(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(deathBody: (DeathBody)value));
            Assert.That(ex.ParamName, Is.EqualTo("deathBody"));
        }

        [TestCase(-1)]
        [TestCase(2)]
        public void RejectsAnUndefinedDeathCamera(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(deathCamera: (DeathCamera)value));
            Assert.That(ex.ParamName, Is.EqualTo("deathCamera"));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANonPositiveOrNonFiniteWaveInterval(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(waveIntervalSeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("waveIntervalSeconds"));
        }

        [Test]
        public void RejectsAnInvalidWaveIntervalEvenWhenNotInWaveMode()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => Rules(respawnMode: RespawnMode.Individual, waveIntervalSeconds: 0f));
            Assert.That(ex.ParamName, Is.EqualTo("waveIntervalSeconds"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteRegenDelay(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(regenDelaySeconds: value));
            Assert.That(ex.ParamName, Is.EqualTo("regenDelaySeconds"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteRegenRate(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(regenPerSecond: value));
            Assert.That(ex.ParamName, Is.EqualTo("regenPerSecond"));
        }

        [TestCase(-0.01f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsANegativeOrNonFiniteReserveAmmoMultiplier(float value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => Rules(reserveAmmoMultiplier: value));
            Assert.That(ex.ParamName, Is.EqualTo("reserveAmmoMultiplier"));
        }

        [Test]
        public void TeamDeathmatchSpecUsesTheDefaultRulesWhenNoneAreGiven()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f);

            Assert.That(spec.Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void TeamDeathmatchSpecUsesTheDefaultRulesWhenNullIsGiven()
        {
            var spec = new TeamDeathmatchSpec(10, 60f, 3f, null);

            Assert.That(spec.Rules, Is.SameAs(GameRules.Default));
        }

        [Test]
        public void TeamDeathmatchSpecKeepsTheGivenRules()
        {
            var rules = Rules(friendlyFire: FriendlyFire.On, respawnMode: RespawnMode.Wave);

            var spec = new TeamDeathmatchSpec(10, 60f, 3f, rules);

            Assert.That(spec.Rules, Is.SameAs(rules));
        }
    }
}

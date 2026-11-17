using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class MineCoreTests
    {
        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = MineSpec.Default;

            Assert.That(spec.ArmSeconds, Is.EqualTo(MineSpec.DefaultArmSeconds));
            Assert.That(spec.TriggerRadius, Is.EqualTo(MineSpec.DefaultTriggerRadius));
            Assert.That(spec.Explosion.Damage.MaxDamage, Is.EqualTo(MineSpec.DefaultMaxDamage));
            Assert.That(spec.Explosion.Damage.Radius, Is.EqualTo(MineSpec.DefaultDamageRadius));
            Assert.That(spec.Explosion.Damage.FullDamageRadius, Is.EqualTo(MineSpec.DefaultFullDamageRadius));
            Assert.That(spec.Explosion.StructureDamageAtCenter, Is.EqualTo(MineSpec.DefaultStructureDamageAtCenter));
        }

        [TestCase(-1f, 1f)]
        [TestCase(float.NaN, 1f)]
        [TestCase(float.PositiveInfinity, 1f)]
        [TestCase(1f, 0f)]
        [TestCase(1f, -1f)]
        [TestCase(1f, float.NaN)]
        [TestCase(1f, float.PositiveInfinity)]
        public void InvalidSpecsAreRejected(float armSeconds, float triggerRadius)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new MineSpec(armSeconds, triggerRadius, MineSpec.Default.Explosion));
        }

        [Test]
        public void AMissingExplosionIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => new MineSpec(1f, 1f, null));
        }

        [Test]
        public void ItArmsOnlyAfterTheArmTimeAndAnnouncesItOnce()
        {
            var state = new MineState(Spec(armSeconds: 1f));

            Assert.That(state.Tick(0.6f), Is.False);
            Assert.That(state.IsArmed, Is.False);
            Assert.That(state.SecondsUntilArmed, Is.EqualTo(0.4f).Within(1e-5f));

            Assert.That(state.Tick(0.6f), Is.True);
            Assert.That(state.IsArmed, Is.True);
            Assert.That(state.SecondsUntilArmed, Is.Zero);

            Assert.That(state.Tick(1f), Is.False, "being armed is announced once");
            Assert.That(state.IsArmed, Is.True);
        }

        [Test]
        public void AZeroArmTimeArmsOnTheFirstTick()
        {
            var state = new MineState(Spec(armSeconds: 0f));

            Assert.That(state.Tick(0f), Is.True);
            Assert.That(state.IsArmed, Is.True);
        }

        [Test]
        public void AnEnemyInRangeDoesNotTriggerItBeforeItIsArmed()
        {
            var state = new MineState(Spec(armSeconds: 1f));
            state.Tick(0.99f);

            Assert.That(state.TryTrigger(true), Is.False);
            Assert.That(state.Triggered, Is.False);
        }

        [Test]
        public void ItTriggersOnceWhenArmedAndAnEnemyIsInRange()
        {
            var state = new MineState(Spec(armSeconds: 0.5f));
            state.Tick(0.5f);

            Assert.That(state.TryTrigger(false), Is.False);
            Assert.That(state.TryTrigger(true), Is.True);
            Assert.That(state.Triggered, Is.True);
            Assert.That(state.IsArmed, Is.False);
            Assert.That(state.TryTrigger(true), Is.False, "a mine goes off only once");
            Assert.That(state.Tick(1f), Is.False);
        }

        [Test]
        public void ANegativeTickIsRejected()
        {
            var state = new MineState(MineSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(float.NaN));
        }

        [Test]
        public void OnlyEnemiesTriggerIt()
        {
            Assert.That(MineState.IsTriggeredBy(true, Side.Alpha, Side.Bravo, true), Is.False, "the owner does not set it off");
            Assert.That(MineState.IsTriggeredBy(false, Side.Alpha, Side.Alpha, true), Is.False, "allies do not set it off");
            Assert.That(MineState.IsTriggeredBy(false, Side.Alpha, Side.Bravo, false), Is.True);
        }

        [Test]
        public void WithoutSidesOnlyBotsTriggerIt()
        {
            Assert.That(MineState.IsTriggeredBy(false, null, null, true), Is.True);
            Assert.That(MineState.IsTriggeredBy(false, null, null, false), Is.False, "players and dummies without a side do not set it off");
            Assert.That(MineState.IsTriggeredBy(true, null, null, true), Is.False);
        }

        [Test]
        public void TheLogsNameTheMineAndWhoSetItOff()
        {
            Assert.That(
                MineLog.Arming("P1", "deploy-mine", 2, 1.5f, 1f, 150, 4f),
                Is.EqualTo("[Mine] owner=P1 kind=deploy-mine serial=2 arming seconds=1.5 trigger=1.0 dmg=150 radius=4.0"));
            Assert.That(MineLog.Armed("P1", "deploy-mine", 2), Is.EqualTo("[Mine] owner=P1 kind=deploy-mine serial=2 armed"));
            Assert.That(
                MineLog.Triggered("P1", "deploy-mine", 2, null, new Vector3(1f, 0.1f, -2f)),
                Is.EqualTo("[Mine] owner=P1 kind=deploy-mine serial=2 triggered by=- at=(1.00, 0.10, -2.00)"));
        }

        private static MineSpec Spec(float armSeconds)
        {
            return new MineSpec(armSeconds, MineSpec.DefaultTriggerRadius, MineSpec.Default.Explosion);
        }
    }
}

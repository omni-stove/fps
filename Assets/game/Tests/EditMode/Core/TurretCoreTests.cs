using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class TurretCoreTests
    {
        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = TurretSpec.Default;

            Assert.That(spec.Rounds, Is.EqualTo(TurretSpec.DefaultRounds));
            Assert.That(spec.LifetimeSeconds, Is.EqualTo(TurretSpec.DefaultLifetimeSeconds));
            Assert.That(spec.SightRange, Is.EqualTo(TurretSpec.DefaultSightRange));
            Assert.That(spec.TurnDegreesPerSecond, Is.EqualTo(TurretSpec.DefaultTurnDegreesPerSecond));
            Assert.That(spec.AimToleranceDegrees, Is.EqualTo(TurretSpec.DefaultAimToleranceDegrees));
        }

        [TestCase(0, 1f, 1f, 1f, 1f)]
        [TestCase(1, 0f, 1f, 1f, 1f)]
        [TestCase(1, float.PositiveInfinity, 1f, 1f, 1f)]
        [TestCase(1, 1f, 0f, 1f, 1f)]
        [TestCase(1, 1f, float.NaN, 1f, 1f)]
        [TestCase(1, 1f, 1f, 0f, 1f)]
        [TestCase(1, 1f, 1f, 1f, -1f)]
        [TestCase(1, 1f, 1f, 1f, 181f)]
        [TestCase(1, 1f, 1f, 1f, float.NaN)]
        public void InvalidSpecsAreRejected(int rounds, float seconds, float sight, float turn, float tolerance)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TurretSpec(rounds, seconds, sight, turn, tolerance));
        }

        [Test]
        public void FiringUsesARoundAndWaitsForTheWeaponsInterval()
        {
            var state = new TurretState(new TurretSpec(3, 10f, 10f, 90f, 5f), 0.5f);

            Assert.That(state.IsReady, Is.True);
            state.Fire();
            Assert.That(state.RoundsLeft, Is.EqualTo(2));
            Assert.That(state.IsReady, Is.False);
            Assert.Throws<InvalidOperationException>(() => state.Fire());

            state.Tick(0.4f);
            Assert.That(state.IsReady, Is.False);
            state.Tick(0.1f);
            Assert.That(state.IsReady, Is.True);
            Assert.That(state.Over, Is.Null);
        }

        [Test]
        public void TheLastRoundEndsItAsExpended()
        {
            var state = new TurretState(new TurretSpec(1, 10f, 10f, 90f, 5f), 0.1f);

            state.Fire();

            Assert.That(state.RoundsLeft, Is.Zero);
            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expended));
            state.Tick(1f);
            Assert.That(state.IsReady, Is.False, "nothing left to fire");
        }

        [Test]
        public void TheLifetimeEndsItAsExpired()
        {
            var state = new TurretState(new TurretSpec(5, 1f, 10f, 90f, 5f), 0.1f);

            state.Tick(0.6f);
            Assert.That(state.Over, Is.Null);
            Assert.That(state.SecondsLeft, Is.EqualTo(0.4f).Within(1e-5f));

            state.Tick(0.6f);
            Assert.That(state.SecondsLeft, Is.Zero);
            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expired));
            Assert.That(state.IsReady, Is.False);
        }

        [Test]
        public void RunningOutAndExpiringTogetherCountsAsExpended()
        {
            var state = new TurretState(new TurretSpec(1, 0.1f, 10f, 90f, 5f), 0.1f);

            state.Fire();
            state.Tick(1f);

            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expended));
        }

        [Test]
        public void TheStateRejectsBadValues()
        {
            Assert.Throws<ArgumentNullException>(() => new TurretState(null, 0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new TurretState(TurretSpec.Default, 0f));
            var state = new TurretState(TurretSpec.Default, 0.1f);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(float.NaN));
        }

        [Test]
        public void TheOwnerAndTheSameSideAreAllies()
        {
            Assert.That(TurretTargeting.IsAlly(true, null, null), Is.True);
            Assert.That(TurretTargeting.IsAlly(false, Side.Alpha, Side.Alpha), Is.True);
            Assert.That(TurretTargeting.IsAlly(false, Side.Alpha, Side.Bravo), Is.False);
            Assert.That(TurretTargeting.IsAlly(false, Side.Alpha, null), Is.False);
            Assert.That(TurretTargeting.IsAlly(false, null, null), Is.False);
        }

        [Test]
        public void HostilesAreOtherSidesOrBotsWhenASideIsMissing()
        {
            Assert.That(TurretTargeting.IsHostile(false, Side.Alpha, Side.Bravo, false), Is.True, "another side");
            Assert.That(TurretTargeting.IsHostile(false, Side.Alpha, Side.Alpha, true), Is.False, "a bot on the same side");
            Assert.That(TurretTargeting.IsHostile(true, Side.Alpha, Side.Bravo, true), Is.False, "the owner");
            Assert.That(TurretTargeting.IsHostile(false, null, null, true), Is.True, "a bot without sides");
            Assert.That(TurretTargeting.IsHostile(false, null, null, false), Is.False, "a player or a dummy without sides");
            Assert.That(TurretTargeting.IsHostile(false, Side.Alpha, null, true), Is.True);
            Assert.That(TurretTargeting.IsHostile(false, null, Side.Bravo, false), Is.False);
        }

        [Test]
        public void OnlyFriendlyFireOnShootsThroughAnAlly()
        {
            Assert.That(TurretTargeting.HoldsFire(FriendlyFire.Off, true), Is.True);
            Assert.That(TurretTargeting.HoldsFire(FriendlyFire.Reflect, true), Is.True);
            Assert.That(TurretTargeting.HoldsFire(FriendlyFire.On, true), Is.False);
            Assert.That(TurretTargeting.HoldsFire(FriendlyFire.Off, false), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => TurretTargeting.HoldsFire((FriendlyFire)99, true));
        }

        [Test]
        public void TheNearestLivingHostileIsChosen()
        {
            var options = new[]
            {
                new TargetOption(true, new Vector3(0f, 0f, 8f), true),
                new TargetOption(false, new Vector3(0f, 0f, 1f), true),
                new TargetOption(true, new Vector3(0f, 0f, 2f), false),
                new TargetOption(true, new Vector3(0f, 0f, 5f), true),
                new TargetOption(true, new Vector3(5f, 0f, 0f), true),
            };

            Assert.That(TurretTargeting.Nearest(Vector3.Zero, options), Is.EqualTo(3), "ties go to the earlier one");
            Assert.That(TurretTargeting.Nearest(Vector3.Zero, new[] { options[1], options[2] }), Is.EqualTo(-1));
            Assert.Throws<ArgumentNullException>(() => TurretTargeting.Nearest(Vector3.Zero, null));
        }

        [Test]
        public void TheHeadTurnsTheShortWayAndStopsOnTheTarget()
        {
            Assert.That(TurretTargeting.StepYaw(350f, 10f, 5f), Is.EqualTo(355f).Within(1e-4f));
            Assert.That(TurretTargeting.StepYaw(10f, 350f, 5f), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(TurretTargeting.StepYaw(0f, 3f, 5f), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(TurretTargeting.StepYaw(0f, -90f, 180f), Is.EqualTo(270f).Within(1e-4f));
            Assert.That(TurretTargeting.StepYaw(0f, 90f, 0f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(TurretTargeting.DeltaDegrees(170f, -170f), Is.EqualTo(20f).Within(1e-4f));
            Assert.That(TurretTargeting.DeltaDegrees(0f, 180f), Is.EqualTo(180f).Within(1e-4f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TurretTargeting.StepYaw(0f, 10f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => TurretTargeting.StepYaw(float.NaN, 10f, 1f));
        }

        [Test]
        public void TheLogLinesCarryTheTagAndTheTurret()
        {
            Assert.That(
                TurretLog.Armed("p", "turret", 2, "rifle", 60, 30f, FriendlyFire.Off),
                Is.EqualTo("[Turret] owner=p kind=turret serial=2 armed weapon=rifle rounds=60 seconds=30.0 friendlyFire=Off"));
            Assert.That(TurretLog.Target("p", "turret", 2, null), Is.EqualTo("[Turret] owner=p kind=turret serial=2 target=-"));
            Assert.That(
                TurretLog.Fired("p", "turret", 2, "e", "e", 25, 59),
                Is.EqualTo("[Turret] owner=p kind=turret serial=2 fire target=e hit=e dmg=25 rounds=59"));
            Assert.That(TurretLog.Killed("p", "turret", 2, "e"), Is.EqualTo("[Turret] owner=p kind=turret serial=2 killed id=e"));
            Assert.That(
                TurretLog.Held("p", "turret", 2, "a", FriendlyFire.Reflect),
                Is.EqualTo("[Turret] owner=p kind=turret serial=2 hold ally=a friendlyFire=Reflect"));
            Assert.That(
                DeployLog.Removed("p", "turret", 2, DeployRemoval.Expended),
                Is.EqualTo("[Deploy] owner=p kind=turret serial=2 removed reason=Expended"));
        }
    }
}

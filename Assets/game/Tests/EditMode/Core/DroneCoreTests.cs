using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class DroneCoreTests
    {
        [Test]
        public void TheDefaultSpecIsValid()
        {
            var spec = DroneSpec.Default;

            Assert.That(spec.Speed, Is.EqualTo(6f));
            Assert.That(spec.ControlRange, Is.EqualTo(40f));
            Assert.That(spec.BatterySeconds, Is.EqualTo(30f));
            Assert.That(spec.SpotRadius, Is.EqualTo(12f));
            Assert.That(spec.SpotIntervalSeconds, Is.EqualTo(1f));
            Assert.That(spec.RevealSeconds, Is.EqualTo(3f));
            Assert.That(spec.RevealSeconds, Is.GreaterThan(spec.SpotIntervalSeconds), "a spotted enemy stays marked until the next sweep");
        }

        [TestCase(0f, 1f, 1f, 1f, 1f, 1f, "speed")]
        [TestCase(1f, -1f, 1f, 1f, 1f, 1f, "controlRange")]
        [TestCase(1f, 1f, float.PositiveInfinity, 1f, 1f, 1f, "batterySeconds")]
        [TestCase(1f, 1f, 1f, float.NaN, 1f, 1f, "spotRadius")]
        [TestCase(1f, 1f, 1f, 1f, 0f, 1f, "spotIntervalSeconds")]
        [TestCase(1f, 1f, 1f, 1f, 1f, -0.5f, "revealSeconds")]
        public void InvalidSpecsAreRejectedAtTheirParameter(
            float speed, float controlRange, float batterySeconds, float spotRadius, float spotIntervalSeconds, float revealSeconds, string param)
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(
                () => new DroneSpec(speed, controlRange, batterySeconds, spotRadius, spotIntervalSeconds, revealSeconds));

            Assert.That(e.ParamName, Is.EqualTo(param));
        }

        [Test]
        public void TheFirstTickSweepsAndThenEachInterval()
        {
            var state = new DroneState(new DroneSpec(5f, 20f, 10f, 8f, 2f, 3f));

            Assert.That(state.Tick(0f, 0f), Is.EqualTo(1), "it looks around as soon as it is up");
            Assert.That(state.Tick(1f, 0f), Is.Zero);
            Assert.That(state.Tick(1f, 0f), Is.EqualTo(1));
            Assert.That(state.Tick(4.5f, 0f), Is.EqualTo(2), "a long tick catches up on the sweeps it skipped");
            Assert.That(state.Sweeps, Is.EqualTo(4));
        }

        [Test]
        public void TheBatteryRunsOutAndEndsIt()
        {
            var state = new DroneState(new DroneSpec(5f, 20f, 4f, 8f, 2f, 3f));

            state.Tick(1f, 0f);
            Assert.That(state.BatterySecondsLeft, Is.EqualTo(3f));
            Assert.That(state.BatteryFraction, Is.EqualTo(0.75f));
            Assert.That(state.Over, Is.Null);

            Assert.That(state.Tick(5f, 0f), Is.Zero, "no sweep at the moment the battery dies");
            Assert.That(state.BatterySecondsLeft, Is.Zero);
            Assert.That(state.BatteryFraction, Is.Zero);
            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expired));
            Assert.That(state.Tick(1f, 0f), Is.Zero, "a dead drone does not sweep again");
        }

        [Test]
        public void FlyingPastTheControlRangeEndsIt()
        {
            var state = new DroneState(new DroneSpec(5f, 20f, 30f, 8f, 2f, 3f));

            state.Tick(0.1f, 15f);
            Assert.That(state.RangeFraction, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(state.Over, Is.Null);

            state.Tick(0.1f, 20f);
            Assert.That(state.RangeFraction, Is.Zero);
            Assert.That(state.Over, Is.Null, "the edge of the range is still in range");

            Assert.That(state.Tick(0.1f, 20.5f), Is.Zero);
            Assert.That(state.Over, Is.EqualTo(DeployRemoval.OutOfRange));
            Assert.That(state.RangeFraction, Is.Zero);
        }

        [Test]
        public void ARunOutBatteryWinsOverTheRangeInTheSameTick()
        {
            var state = new DroneState(new DroneSpec(5f, 20f, 1f, 8f, 2f, 3f));

            state.Tick(2f, 25f);

            Assert.That(state.Over, Is.EqualTo(DeployRemoval.Expired));
        }

        [Test]
        public void TheStatusCarriesTheFractionsTheSpottedCountAndWhetherItIsPiloted()
        {
            var state = new DroneState(new DroneSpec(5f, 20f, 10f, 8f, 2f, 3f));
            state.Tick(2.5f, 5f);
            state.CountSpotted(2);
            state.CountSpotted(0);

            var piloted = state.Status(true);
            Assert.That(piloted.BatteryFraction, Is.EqualTo(0.75f));
            Assert.That(piloted.RangeFraction, Is.EqualTo(0.75f));
            Assert.That(piloted.SpottedCount, Is.EqualTo(2));
            Assert.That(piloted.Piloting, Is.True);
            Assert.That(state.Status(false).Piloting, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.CountSpotted(-1));
        }

        [Test]
        public void BadTicksAreRejected()
        {
            var state = new DroneState(DroneSpec.Default);

            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(-1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(float.NaN, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(0.1f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Tick(0.1f, float.PositiveInfinity));
            Assert.Throws<ArgumentNullException>(() => new DroneState(null));
        }

        [Test]
        public void TheLogLinesNameTheDroneAndWhatHappened()
        {
            Assert.That(
                DroneLog.Armed("Owner", "drone", 1, new DroneSpec(6f, 40f, 30f, 12f, 1f, 3f)),
                Is.EqualTo("[Drone] owner=Owner kind=drone serial=1 armed speed=6.0 range=40.0 battery=30.0 radius=12.0 interval=1.00 reveal=3.0"));
            Assert.That(
                DroneLog.Spotted("Owner", "drone", 1, "Enemy", new Vector3(1f, 0f, -2.5f), true),
                Is.EqualTo("[Drone] owner=Owner kind=drone serial=1 spotted target=Enemy at=(1.00, 0.00, -2.50) marked=yes"));
            Assert.That(DroneLog.Spotted(null, "drone", 2, null, Vector3.Zero, false), Does.EndWith("target=- at=(0.00, 0.00, 0.00) marked=no"));
            Assert.That(DroneLog.PilotStarted("Owner", "drone", 1), Is.EqualTo("[Drone] owner=Owner kind=drone serial=1 piloting started"));
            Assert.That(
                DroneLog.PilotEnded("Owner", "drone", 1, DronePilotEnd.Interact),
                Is.EqualTo("[Drone] owner=Owner kind=drone serial=1 piloting ended reason=Interact"));
            Assert.That(
                DroneLog.Recalled("Owner", "drone", 1, new Vector3(2f, 3f, 4f)),
                Is.EqualTo("[Drone] owner=Owner kind=drone serial=1 recalled at=(2.00, 3.00, 4.00)"));
            Assert.That(
                DroneLog.PilotRefused("Bot", "drone", 3, "PlayerController"),
                Is.EqualTo("[Drone] owner=Bot kind=drone serial=3 piloting refused missing=PlayerController"));
        }

        [Test]
        public void TheDefaultShapeOfADroneComesFromItsSpec()
        {
            var deploy = DeployType.DefaultDeploy(DeployRole.Drone);

            Assert.That(deploy.MaxActive, Is.EqualTo(DroneSpec.DefaultMaxActive));
            Assert.That(deploy.MaxHealth, Is.EqualTo(DroneSpec.DefaultMaxHealth));
            Assert.That(deploy.Width, Is.EqualTo(DroneSpec.DefaultWidth));
            Assert.That(deploy.Height, Is.EqualTo(DroneSpec.DefaultHeight));
            Assert.That(deploy.Depth, Is.EqualTo(DroneSpec.DefaultDepth));
            Assert.That(deploy.Overflow, Is.EqualTo(DeploySpec.DefaultOverflow));
        }

        [Test]
        public void ADroneTypeNeedsADroneSpecAndOnlyADroneHasOne()
        {
            Assert.Throws<ArgumentException>(() => new DeployType("d", DeployRole.Drone, DeploySpec.Default));
            Assert.Throws<ArgumentException>(() => new DeployType("s", DeployRole.Shield, DeploySpec.Default, drone: DroneSpec.Default));
            Assert.That(new DeployType("d", DeployRole.Drone, DeploySpec.Default, drone: DroneSpec.Default).Drone, Is.SameAs(DroneSpec.Default));
            Assert.That(new DeployType("s", DeployRole.Shield, DeploySpec.Default).Drone, Is.Null);
        }
    }
}

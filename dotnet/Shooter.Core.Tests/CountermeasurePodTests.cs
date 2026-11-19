using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CountermeasurePodTests
    {
        private static readonly Vector3 _gravity = new Vector3(0f, -10f, 0f);

        [Test]
        public void StartsWithTheConfiguredCountAndNothingInTheAir()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 4));

            Assert.That(pod.Remaining, Is.EqualTo(4));
            Assert.That(pod.CooldownLeft, Is.Zero);
            Assert.That(pod.Flares, Is.Empty);
            Assert.That(pod.Readiness, Is.EqualTo(CountermeasureVerdict.Deployed));
        }

        [Test]
        public void DeployingSpendsOneFlareAndPutsItInTheAir()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 3, intervalSeconds: 0f));

            var verdict = pod.TryDeploy(new Vector3(1f, 2f, 3f), new Vector3(0f, 0f, -5f), out var id);

            Assert.That(verdict, Is.EqualTo(CountermeasureVerdict.Deployed));
            Assert.That(id, Is.EqualTo(0));
            Assert.That(pod.Remaining, Is.EqualTo(2));
            Assert.That(pod.Flares, Has.Count.EqualTo(1));
            Assert.That(pod.Flares[0].Position, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(pod.TryLocate(id, out var point), Is.True);
            Assert.That(point, Is.EqualTo(new Vector3(1f, 2f, 3f)));

            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out var second);
            Assert.That(second, Is.EqualTo(1), "every flare gets its own id");
        }

        [Test]
        public void NothingIsDeployedOnceTheCountRunsOut()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 2, intervalSeconds: 0f));
            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _);
            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _);

            var verdict = pod.TryDeploy(Vector3.Zero, Vector3.Zero, out var id);

            Assert.That(verdict, Is.EqualTo(CountermeasureVerdict.Empty));
            Assert.That(id, Is.EqualTo(-1));
            Assert.That(pod.Remaining, Is.Zero);
            Assert.That(pod.Flares, Has.Count.EqualTo(2));
            Assert.That(pod.Readiness, Is.EqualTo(CountermeasureVerdict.Empty));
        }

        [Test]
        public void AZeroCountNeverDeploys()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 0));

            Assert.That(pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _), Is.EqualTo(CountermeasureVerdict.Empty));
            Assert.That(pod.Flares, Is.Empty);
        }

        [Test]
        public void NothingIsDeployedUntilTheIntervalHasPassed()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 3, intervalSeconds: 1f));
            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _);

            Assert.That(pod.TryDeploy(Vector3.Zero, Vector3.Zero, out var refused), Is.EqualTo(CountermeasureVerdict.CoolingDown));
            Assert.That(refused, Is.EqualTo(-1));
            Assert.That(pod.Remaining, Is.EqualTo(2), "a refused press does not spend a flare");

            pod.Tick(0.5f, _gravity);
            Assert.That(pod.CooldownLeft, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _), Is.EqualTo(CountermeasureVerdict.CoolingDown));

            pod.Tick(0.5f, _gravity);
            Assert.That(pod.CooldownLeft, Is.Zero);
            Assert.That(pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _), Is.EqualTo(CountermeasureVerdict.Deployed));
            Assert.That(pod.Remaining, Is.EqualTo(1));
        }

        [Test]
        public void AnEmptyPodSaysEmptyEvenWhileCoolingDown()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 1, intervalSeconds: 5f));
            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _);

            Assert.That(pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _), Is.EqualTo(CountermeasureVerdict.Empty));
        }

        [Test]
        public void FlaresDriftWithTheirVelocityAndScaledGravityThenBurnOut()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(lifetimeSeconds: 4f, gravityMultiplier: 0.5f));
            pod.TryDeploy(Vector3.Zero, new Vector3(0f, 0f, -10f), out var id);

            pod.Tick(2f, _gravity);

            Assert.That(pod.TryLocate(id, out var point), Is.True);
            Assert.That(Vector3.Distance(point, new Vector3(0f, -10f, -20f)), Is.LessThan(1e-4f), "z = -10 * 2, y = 0.5 * -5 * 2^2");
            Assert.That(pod.Flares[0].Velocity.Y, Is.EqualTo(-10f).Within(1e-4f));
            Assert.That(pod.Flares[0].Age, Is.EqualTo(2f));

            pod.Tick(2f, _gravity);

            Assert.That(pod.Flares, Is.Empty);
            Assert.That(pod.TryLocate(id, out _), Is.False);
        }

        [Test]
        public void RefillingRestoresTheCountAndKeepsTheFlaresInTheAir()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(count: 1));
            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _);

            pod.Refill();

            Assert.That(pod.Remaining, Is.EqualTo(1));
            Assert.That(pod.Flares, Has.Count.EqualTo(1));
        }

        [Test]
        public void LuresOnlyASeekerThatSeesTheFlareNearItsTarget()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(decoyConeDegrees: 10f, intervalSeconds: 0f));
            var target = new Vector3(0f, 0f, 100f);

            Assert.That(pod.TryLure(Vector3.Zero, target, out _, out _), Is.False, "nothing deployed yet");

            pod.TryDeploy(new Vector3(100f, 0f, 0f), Vector3.Zero, out _);
            Assert.That(pod.TryLure(Vector3.Zero, target, out _, out _), Is.False, "the flare is 90 degrees off the target");

            pod.TryDeploy(new Vector3(0f, -5f, 100f), Vector3.Zero, out var near);
            Assert.That(pod.TryLure(Vector3.Zero, target, out var id, out var point), Is.True);
            Assert.That(id, Is.EqualTo(near));
            Assert.That(point, Is.EqualTo(new Vector3(0f, -5f, 100f)));
        }

        [Test]
        public void TheFlareClosestToTheTargetLuresAndTheNewerOneWinsATie()
        {
            var pod = new CountermeasurePod(new CountermeasureSpec(decoyConeDegrees: 30f, intervalSeconds: 0f));
            var target = new Vector3(0f, 0f, 100f);
            pod.TryDeploy(new Vector3(0f, -20f, 100f), Vector3.Zero, out _);
            pod.TryDeploy(new Vector3(0f, -5f, 100f), Vector3.Zero, out var closer);

            Assert.That(pod.TryLure(Vector3.Zero, target, out var id, out _), Is.True);
            Assert.That(id, Is.EqualTo(closer));

            pod.TryDeploy(new Vector3(0f, -5f, 100f), Vector3.Zero, out var newer);
            pod.TryLure(Vector3.Zero, target, out id, out _);
            Assert.That(id, Is.EqualTo(newer));
        }

        [Test]
        public void ASeekerSittingOnItsTargetIsNotLured()
        {
            var pod = new CountermeasurePod(CountermeasureSpec.Default);
            pod.TryDeploy(Vector3.Zero, Vector3.Zero, out _);

            Assert.That(pod.TryLure(Vector3.UnitZ, Vector3.UnitZ, out var id, out _), Is.False);
            Assert.That(id, Is.EqualTo(-1));
        }

        [Test]
        public void TheDefaultSpecUsesTheDefaults()
        {
            var spec = CountermeasureSpec.Default;

            Assert.That(spec.Count, Is.EqualTo(CountermeasureSpec.DefaultCount));
            Assert.That(spec.IntervalSeconds, Is.EqualTo(CountermeasureSpec.DefaultIntervalSeconds));
            Assert.That(spec.LifetimeSeconds, Is.EqualTo(CountermeasureSpec.DefaultLifetimeSeconds));
            Assert.That(spec.DecoyConeDegrees, Is.EqualTo(CountermeasureSpec.DefaultDecoyConeDegrees));
            Assert.That(spec.EjectSpeed, Is.EqualTo(CountermeasureSpec.DefaultEjectSpeed));
            Assert.That(spec.GravityMultiplier, Is.EqualTo(CountermeasureSpec.DefaultGravityMultiplier));
        }

        [Test]
        public void TheSpecRejectsValuesThatCannotWork()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(count: -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(intervalSeconds: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(lifetimeSeconds: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(lifetimeSeconds: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(decoyConeDegrees: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(decoyConeDegrees: 91f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(ejectSpeed: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CountermeasureSpec(gravityMultiplier: -0.1f));
        }

        [Test]
        public void ThePodRejectsBadInput()
        {
            Assert.Throws<ArgumentNullException>(() => new CountermeasurePod(null));
            var pod = new CountermeasurePod(CountermeasureSpec.Default);
            Assert.Throws<ArgumentOutOfRangeException>(() => pod.TryDeploy(new Vector3(float.NaN, 0f, 0f), Vector3.Zero, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => pod.Tick(-1f, _gravity));
            Assert.Throws<ArgumentOutOfRangeException>(() => pod.Tick(0.1f, new Vector3(0f, float.PositiveInfinity, 0f)));
            Assert.That(pod.Remaining, Is.EqualTo(CountermeasureSpec.DefaultCount), "a rejected deploy spends nothing");
        }
    }
}

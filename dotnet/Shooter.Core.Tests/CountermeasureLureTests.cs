using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CountermeasureLureTests
    {
        private const int _everything = ~0;
        private const float _step = 0.25f;

        private static readonly HomingSpec _homing = new HomingSpec(lockOnSeconds: 1f, lockOnConeDegrees: 10f, turnDegreesPerSecond: 180f);
        private static readonly Vector3 _target = new Vector3(0f, 0f, 40f);
        private static readonly Vector3 _flare = new Vector3(0f, -3f, 30f);

        [Test]
        public void DeployingAFlareBreaksTheLock()
        {
            var pod = StillFlares(decoyConeDegrees: 30f);
            var tracker = new LockOnTracker();
            var targets = new[] { new LockOnCandidate(7, _target, pod) };
            for (var i = 0; i < 4; i++)
            {
                tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);
            }

            Assert.That(tracker.LockedTargetId, Is.EqualTo(7));

            pod.TryDeploy(_flare, Vector3.Zero, out _);
            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);

            Assert.That(tracker.TargetId, Is.Null);
            Assert.That(tracker.IsLocked, Is.False);
            Assert.That(tracker.Progress, Is.Zero);
        }

        [Test]
        public void TheTargetIsTrackedFromZeroAgainAfterTheFlareBurnsOut()
        {
            var pod = StillFlares(decoyConeDegrees: 30f, lifetimeSeconds: 1f);
            var tracker = new LockOnTracker();
            var targets = new[] { new LockOnCandidate(7, _target, pod) };
            pod.TryDeploy(_flare, Vector3.Zero, out _);
            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);
            Assert.That(tracker.TargetId, Is.Null);

            pod.Tick(1f, Vector3.Zero);
            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);

            Assert.That(tracker.TargetId, Is.EqualTo(7));
            Assert.That(tracker.HeldSeconds, Is.EqualTo(_step));
            Assert.That(tracker.IsLocked, Is.False);
        }

        [Test]
        public void AFlareFarFromTheTargetLeavesTheLockAlone()
        {
            var pod = StillFlares(decoyConeDegrees: 5f);
            var tracker = new LockOnTracker();
            var targets = new[] { new LockOnCandidate(7, _target, pod) };
            pod.TryDeploy(new Vector3(20f, 0f, 40f), Vector3.Zero, out _);

            for (var i = 0; i < 4; i++)
            {
                tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);
            }

            Assert.That(tracker.LockedTargetId, Is.EqualTo(7));
        }

        [Test]
        public void TheTrackerMovesOnToAnotherTargetThatIsNotLured()
        {
            var pod = StillFlares(decoyConeDegrees: 30f);
            pod.TryDeploy(_flare, Vector3.Zero, out _);
            var tracker = new LockOnTracker();
            var targets = new[]
            {
                new LockOnCandidate(7, _target, pod),
                new LockOnCandidate(8, new Vector3(-3f, 0f, 40f)),
            };

            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);

            Assert.That(tracker.TargetId, Is.EqualTo(8), "the other one has no flare and is still in the cone");
        }

        [Test]
        public void SeekGoesForTheFlareThatLuresTheSeeker()
        {
            var pod = StillFlares(decoyConeDegrees: 30f);
            pod.TryDeploy(_flare, Vector3.Zero, out var id);

            var aim = ProjectileSteering.Seek(pod, Vector3.Zero, _target, null, out var lure);

            Assert.That(aim, Is.EqualTo(_flare));
            Assert.That(lure, Is.EqualTo(id));
        }

        [Test]
        public void SeekKeepsChasingALuringFlareEvenAfterItLeavesTheCone()
        {
            var pod = StillFlares(decoyConeDegrees: 10f);
            pod.TryDeploy(_flare, Vector3.Zero, out var id);
            var close = new Vector3(0f, -3f, 29f);
            Assert.That(pod.TryLure(close, _target, out _, out _), Is.False, "from here the flare is 15 degrees off the target");

            var aim = ProjectileSteering.Seek(pod, close, _target, id, out var lure);

            Assert.That(aim, Is.EqualTo(_flare));
            Assert.That(lure, Is.EqualTo(id));
        }

        [Test]
        public void SeekGoesBackToTheTargetOnceTheFlareBurnsOut()
        {
            var pod = StillFlares(decoyConeDegrees: 30f, lifetimeSeconds: 1f);
            pod.TryDeploy(_flare, Vector3.Zero, out var id);
            pod.Tick(1f, Vector3.Zero);

            var aim = ProjectileSteering.Seek(pod, Vector3.Zero, _target, id, out var lure);

            Assert.That(aim, Is.EqualTo(_target));
            Assert.That(lure, Is.Null);
        }

        [Test]
        public void SeekWithoutADecoyOrATargetPassesTheTargetThrough()
        {
            Assert.That(ProjectileSteering.Seek(null, Vector3.Zero, _target, 3, out var lure), Is.EqualTo(_target));
            Assert.That(lure, Is.Null);

            var pod = StillFlares(decoyConeDegrees: 30f);
            pod.TryDeploy(_flare, Vector3.Zero, out _);
            Assert.That(ProjectileSteering.Seek(pod, Vector3.Zero, null, null, out lure), Is.Null, "nothing to compare the flare with");
            Assert.That(lure, Is.Null);
        }

        [Test]
        public void AHomingRoundInFlightBendsTowardTheFlare()
        {
            var pod = StillFlares(decoyConeDegrees: 30f);
            var flight = Missile();
            flight.Step(0.05f, _target, pod);
            Assert.That(flight.LuredDecoyId, Is.Null);
            Assert.That(flight.Velocity.Y, Is.EqualTo(0f).Within(1e-5f), "flies straight at the target before the flare");

            pod.TryDeploy(_flare, Vector3.Zero, out var id);
            flight.Step(0.05f, _target, pod);

            Assert.That(flight.LuredDecoyId, Is.EqualTo(id));
            Assert.That(flight.Velocity.Y, Is.LessThan(0f), "turns down toward the flare");

            while (flight.Position.Z < _flare.Z && !flight.IsDone)
            {
                flight.Step(0.05f, _target, pod);
            }

            Assert.That(flight.Position.Y, Is.EqualTo(_flare.Y).Within(0.5f), "reaches the flare instead of the target");
        }

        [Test]
        public void AHomingRoundWithoutADecoyKeepsOnItsTarget()
        {
            var flight = Missile();

            for (var i = 0; i < 10; i++)
            {
                flight.Step(0.05f, _target);
            }

            Assert.That(flight.LuredDecoyId, Is.Null);
            Assert.That(flight.Position.Y, Is.EqualTo(0f).Within(1e-4f));
        }

        private static BallisticFlight Missile()
        {
            return new BallisticFlight(
                new ProjectileSpec(20f, 0f, 10f),
                _homing,
                new FakeWorldCollision(),
                _everything,
                Vector3.Zero,
                Vector3.UnitZ,
                Vector3.Zero);
        }

        private static CountermeasurePod StillFlares(float decoyConeDegrees, float lifetimeSeconds = 10f)
        {
            return new CountermeasurePod(new CountermeasureSpec(
                count: 6, intervalSeconds: 0f, lifetimeSeconds: lifetimeSeconds, decoyConeDegrees: decoyConeDegrees, ejectSpeed: 0f, gravityMultiplier: 0f));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class LockOnTrackerTests
    {
        private const float _step = 0.25f;

        private static readonly HomingSpec _homing = new HomingSpec(lockOnSeconds: 1f, lockOnConeDegrees: 10f, turnDegreesPerSecond: 90f);

        [Test]
        public void LocksAfterHoldingTheTargetInTheConeForTheLockOnSeconds()
        {
            var tracker = new LockOnTracker();
            var targets = new[] { new LockOnCandidate(7, new Vector3(0f, 0f, 20f)) };

            for (var i = 0; i < 3; i++)
            {
                tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);
                Assert.That(tracker.TargetId, Is.EqualTo(7));
                Assert.That(tracker.IsLocked, Is.False, "after " + ((i + 1) * _step) + " s");
                Assert.That(tracker.LockedTargetId, Is.Null);
            }

            Assert.That(tracker.Progress, Is.EqualTo(0.75f).Within(1e-5f));
            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);

            Assert.That(tracker.IsLocked, Is.True);
            Assert.That(tracker.LockedTargetId, Is.EqualTo(7));
            Assert.That(tracker.Progress, Is.EqualTo(1f));
        }

        [Test]
        public void AZeroLockOnTimeLocksAtOnce()
        {
            var tracker = new LockOnTracker();
            tracker.Update(new HomingSpec(0f, 10f, 90f), _step, true, Vector3.Zero, Vector3.UnitZ, new[] { new LockOnCandidate(1, Vector3.UnitZ) });

            Assert.That(tracker.LockedTargetId, Is.EqualTo(1));
        }

        [Test]
        public void ATargetOutsideTheConeIsNotCaught()
        {
            var tracker = new LockOnTracker();

            // 11 度ずれた相手
            var point = new Vector3((float)Math.Tan(11.0 * Math.PI / 180.0) * 20f, 0f, 20f);
            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, new[] { new LockOnCandidate(1, point) });

            Assert.That(tracker.TargetId, Is.Null);
            Assert.That(tracker.Progress, Is.Zero);
        }

        [Test]
        public void LeavingTheConeOrStoppingAimingStartsOver()
        {
            var tracker = new LockOnTracker();
            var inside = new[] { new LockOnCandidate(1, Vector3.UnitZ * 10f) };
            var outside = new[] { new LockOnCandidate(1, Vector3.UnitX * 10f) };

            Hold(tracker, inside, 3);
            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, outside);
            Assert.That(tracker.TargetId, Is.Null);

            Hold(tracker, inside, 3);
            tracker.Update(_homing, _step, false, Vector3.Zero, Vector3.UnitZ, inside);
            Assert.That(tracker.TargetId, Is.Null, "stopped aiming");

            Hold(tracker, inside, 3);
            Assert.That(tracker.IsLocked, Is.False, "the time before letting go is not kept");
        }

        [Test]
        public void CatchesTheTargetNearestTheAimAndKeepsItWhileItStaysInTheCone()
        {
            var tracker = new LockOnTracker();
            var near = new LockOnCandidate(2, new Vector3(0.5f, 0f, 20f));
            var far = new LockOnCandidate(1, new Vector3(2f, 0f, 20f));

            tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, new[] { far, near });
            Assert.That(tracker.TargetId, Is.EqualTo(2));

            // 今の相手がまだ円錐にいれば、もっと狙いに近い相手が来ても乗り換えない
            var nearer = new LockOnCandidate(3, new Vector3(0f, 0f, 20f));
            Hold(tracker, new[] { nearer, near }, 3);
            Assert.That(tracker.LockedTargetId, Is.EqualTo(2));
        }

        [Test]
        public void ChangingTheWeaponStartsOver()
        {
            var tracker = new LockOnTracker();
            var targets = new[] { new LockOnCandidate(1, Vector3.UnitZ) };
            Hold(tracker, targets, 3);

            tracker.Update(new HomingSpec(1f, 10f, 90f), _step, true, Vector3.Zero, Vector3.UnitZ, targets);

            Assert.That(tracker.HeldSeconds, Is.EqualTo(_step));
        }

        [Test]
        public void RejectsInvalidInput()
        {
            var tracker = new LockOnTracker();
            var none = Array.Empty<LockOnCandidate>();

            Assert.Throws<ArgumentNullException>(() => tracker.Update(null, _step, true, Vector3.Zero, Vector3.UnitZ, none));
            Assert.Throws<ArgumentNullException>(() => tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => tracker.Update(_homing, -1f, true, Vector3.Zero, Vector3.UnitZ, none));
        }

        [Test]
        public void IsInConeMeasuresFromTheEyeAlongTheAim()
        {
            var eye = new Vector3(0f, 1.6f, 0f);

            Assert.That(LockOnTracker.IsInCone(_homing, eye, new Vector3(0f, 0f, 3f), eye + new Vector3(1f, 0f, 10f)), Is.True);
            Assert.That(LockOnTracker.IsInCone(_homing, eye, Vector3.UnitZ, eye + new Vector3(3f, 0f, 10f)), Is.False);
            Assert.That(LockOnTracker.IsInCone(_homing, eye, Vector3.Zero, eye + Vector3.UnitZ), Is.False, "no aim");
        }

        private static void Hold(LockOnTracker tracker, IReadOnlyList<LockOnCandidate> targets, int steps)
        {
            for (var i = 0; i < steps; i++)
            {
                tracker.Update(_homing, _step, true, Vector3.Zero, Vector3.UnitZ, targets);
            }
        }
    }
}

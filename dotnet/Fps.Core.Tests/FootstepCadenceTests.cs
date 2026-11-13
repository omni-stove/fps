using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class FootstepCadenceTests
    {
        private const float _tolerance = 1e-4f;
        private const float _frame = 1f / 60f;

        // PlayerController の既定値: 歩き 5、ダッシュ 8、しゃがみ 2.5 (m/s)。
        private const float _walkSpeed = 5f;
        private const float _sprintSpeed = 8f;
        private const float _crouchSpeed = 2.5f;

        [Test]
        public void DefaultsMatchTheConstants()
        {
            var cadence = new FootstepCadence();

            Assert.That(cadence.WalkStride, Is.EqualTo(FootstepCadence.DefaultWalkStride));
            Assert.That(cadence.SprintStride, Is.EqualTo(FootstepCadence.DefaultSprintStride));
            Assert.That(cadence.CrouchStride, Is.EqualTo(FootstepCadence.DefaultCrouchStride));
            Assert.That(cadence.WalkVolume, Is.EqualTo(FootstepCadence.DefaultWalkVolume));
            Assert.That(cadence.SprintVolume, Is.EqualTo(FootstepCadence.DefaultSprintVolume));
            Assert.That(cadence.CrouchVolume, Is.EqualTo(FootstepCadence.DefaultCrouchVolume));
            Assert.That(cadence.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
        }

        [Test]
        public void RejectsInvalidSettings()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(walkStride: 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(sprintStride: -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(crouchStride: float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(walkStride: float.PositiveInfinity));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(walkVolume: -0.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(sprintVolume: 1.1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCadence(crouchVolume: float.NaN));
        }

        [Test]
        public void RejectsInvalidSamples()
        {
            var cadence = new FootstepCadence();

            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.Advance(-0.1f, 0f, _frame, true, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.Advance(float.NaN, 0f, _frame, true, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.Advance(0f, float.PositiveInfinity, _frame, true, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.Advance(0f, 0f, -_frame, true, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.Advance(0f, 0f, float.NaN, true, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => cadence.IntervalFor(-1f, false, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => FootstepCadence.LandingVolume(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => FootstepCadence.IsSprintingBySpeed(1f, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => FootstepCadence.IsSprintingBySpeed(float.NaN, 5f));
        }

        [Test]
        public void CrouchingHasTheShortestStrideAndSprintingTheLongest()
        {
            var cadence = new FootstepCadence();

            Assert.That(cadence.StrideFor(crouching: true, sprinting: false), Is.LessThan(cadence.StrideFor(false, false)));
            Assert.That(cadence.StrideFor(crouching: false, sprinting: true), Is.GreaterThan(cadence.StrideFor(false, false)));
            Assert.That(cadence.StrideFor(crouching: true, sprinting: true), Is.EqualTo(cadence.CrouchStride), "crouch wins over sprint");
        }

        [Test]
        public void CrouchingIsQuieterAndSprintingIsLouder()
        {
            var cadence = new FootstepCadence();

            Assert.That(cadence.VolumeFor(crouching: true, sprinting: false), Is.LessThan(cadence.VolumeFor(false, false)));
            Assert.That(cadence.VolumeFor(crouching: false, sprinting: true), Is.GreaterThan(cadence.VolumeFor(false, false)));
            Assert.That(cadence.VolumeFor(crouching: true, sprinting: true), Is.EqualTo(cadence.CrouchVolume));
        }

        [Test]
        public void SprintingStepsMoreOftenThanWalkingAndCrouchingLessOften()
        {
            var cadence = new FootstepCadence();

            var walk = cadence.IntervalFor(_walkSpeed, false, false);
            var sprint = cadence.IntervalFor(_sprintSpeed, false, true);
            var crouch = cadence.IntervalFor(_crouchSpeed, true, false);

            Assert.That(walk, Is.EqualTo(0.44f).Within(_tolerance));
            Assert.That(sprint, Is.EqualTo(0.325f).Within(_tolerance));
            Assert.That(crouch, Is.EqualTo(0.6f).Within(_tolerance));
            Assert.That(sprint, Is.LessThan(walk));
            Assert.That(crouch, Is.GreaterThan(walk));
        }

        [Test]
        public void SlowerWalkingStepsLessOften()
        {
            var cadence = new FootstepCadence();

            Assert.That(cadence.IntervalFor(2f, false, false), Is.GreaterThan(cadence.IntervalFor(_walkSpeed, false, false)));
            Assert.That(cadence.IntervalFor(0.05f, false, false), Is.EqualTo(float.PositiveInfinity));
        }

        [Test]
        public void StandingStillMakesNoSound()
        {
            var cadence = new FootstepCadence();

            for (var i = 0; i < 600; i++)
            {
                Assert.That(cadence.Advance(0f, 0f, _frame, true, false, false), Is.EqualTo(FootstepCue.None));
            }
        }

        [Test]
        public void FirstStepComesAfterHalfAStrideThenOneEveryStride()
        {
            var cadence = new FootstepCadence();
            var steps = Walk(cadence, _walkSpeed, seconds: 2f, crouching: false, sprinting: false, out var firstAt, out var volume);

            // 2.2m の歩幅の半分 1.1m を 5 m/s で 0.22 秒。その後 0.44 秒ごと: 0.22, 0.66, 1.10, 1.54, 1.98
            Assert.That(firstAt, Is.EqualTo(0.22f).Within(_frame));
            Assert.That(steps, Is.EqualTo(5));
            Assert.That(volume, Is.EqualTo(FootstepCadence.DefaultWalkVolume));
        }

        [Test]
        public void SprintingStepsMoreOftenAndLouderOverTheSameTime()
        {
            var walk = Walk(new FootstepCadence(), _walkSpeed, 4f, false, false, out _, out var walkVolume);
            var sprint = Walk(new FootstepCadence(), _sprintSpeed, 4f, false, true, out _, out var sprintVolume);

            Assert.That(sprint, Is.GreaterThan(walk));
            Assert.That(sprintVolume, Is.GreaterThan(walkVolume));
        }

        [Test]
        public void CrouchWalkingStepsLessOftenAndQuieter()
        {
            var walk = Walk(new FootstepCadence(), _walkSpeed, 4f, false, false, out _, out var walkVolume);
            var crouch = Walk(new FootstepCadence(), _crouchSpeed, 4f, true, false, out _, out var crouchVolume);

            Assert.That(crouch, Is.LessThan(walk));
            Assert.That(crouchVolume, Is.LessThan(walkVolume));
        }

        [Test]
        public void StoppingRestartsFromHalfAStride()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0.9f, 0f, 0.18f, true, false, false);
            Assert.That(cadence.Progress, Is.GreaterThan(FootstepCadence.FirstStepProgress));

            cadence.Advance(0f, 0f, FootstepCadence.StopSeconds, true, false, false);

            Assert.That(cadence.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
        }

        [Test]
        public void FramesWithoutMovementBetweenTicksKeepTheStride()
        {
            var cadence = new FootstepCadence();

            // 30 Hz の tick で動く本体を 120 fps で見ると、4 フレームに 1 回だけ動く
            var tick = 1f / 30f;
            var steps = 0;
            for (var i = 0; i < 480; i++)
            {
                var moved = i % 4 == 0 ? _walkSpeed * tick : 0f;
                if (cadence.Advance(moved, 0f, 1f / 120f, true, false, false).Kind == FootstepCueKind.Footstep)
                {
                    steps++;
                }
            }

            // 4 秒で 20m 歩く: 半歩 + 2.2m ごとで 9 回
            Assert.That(steps, Is.EqualTo(9));
        }

        [Test]
        public void ALongFrameStepsOnlyOnceAndKeepsTheRemainder()
        {
            var cadence = new FootstepCadence(walkStride: 2f);

            // 0.5 + 5m / 2m = 3.0 歩ぶん進んでも鳴らすのは 1 回
            var cue = cadence.Advance(5f, 0f, 1f, true, false, false);

            Assert.That(cue.Kind, Is.EqualTo(FootstepCueKind.Footstep));
            Assert.That(cadence.Progress, Is.EqualTo(0f).Within(_tolerance));
        }

        [Test]
        public void NoFootstepsInTheAir()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0f, 0f, _frame, true, false, false);

            for (var i = 0; i < 120; i++)
            {
                Assert.That(cadence.Advance(_walkSpeed * _frame, 0f, _frame, false, false, false).HasSound, Is.False);
            }
        }

        [Test]
        public void FirstGroundedFrameIsNotALanding()
        {
            var cadence = new FootstepCadence();

            Assert.That(cadence.Advance(0f, -0.2f, _frame, true, false, false), Is.EqualTo(FootstepCue.None));
        }

        [Test]
        public void JumpLandingPlaysTheLandingSound()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0f, 0f, _frame, true, false, false);

            // 1.2m のジャンプは約 7 m/s で戻ってくる。地面に当たったフレームは途中で止まり、縦の動きは小さい
            cadence.Advance(0f, -7f * _frame, _frame, false, false, false);
            var cue = cadence.Advance(0f, -0.01f, _frame, true, false, false);

            Assert.That(cue.Kind, Is.EqualTo(FootstepCueKind.Landing));
            Assert.That(cue.Volume, Is.EqualTo(FootstepCadence.LandingVolume(7f)).Within(_tolerance));
            Assert.That(cadence.Progress, Is.EqualTo(0f), "the next footstep waits a whole stride after landing");
        }

        [Test]
        public void SmallDropIsNotALandingAndKeepsTheStride()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0.2f, 0f, _frame, true, false, false);
            var progress = cadence.Progress;

            cadence.Advance(0f, -0.02f, _frame, false, false, false);
            var cue = cadence.Advance(0f, -0.01f, _frame, true, false, false);

            Assert.That(cue, Is.EqualTo(FootstepCue.None));
            Assert.That(cadence.Progress, Is.EqualTo(progress));
        }

        [Test]
        public void SmallDropWhileWalkingStillAdvancesTheStride()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0.2f, 0f, _frame, true, false, false);
            var progress = cadence.Progress;

            cadence.Advance(0.08f, -0.02f, _frame, false, false, false);
            cadence.Advance(0.08f, -0.01f, _frame, true, false, false);

            Assert.That(cadence.Progress, Is.EqualTo(progress + (0.08f / cadence.WalkStride)).Within(_tolerance));
        }

        [TestCase(0f, 0f)]
        [TestCase(2.4f, 0f)]
        [TestCase(2.5f, FootstepCadence.MinLandingVolume)]
        [TestCase(12f, 1f)]
        [TestCase(40f, 1f)]
        public void LandingVolumeGrowsWithTheFallSpeed(float fallSpeed, float expected)
        {
            Assert.That(FootstepCadence.LandingVolume(fallSpeed), Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void HarderLandingsAreLouder()
        {
            Assert.That(FootstepCadence.LandingVolume(9f), Is.GreaterThan(FootstepCadence.LandingVolume(5f)));
        }

        [Test]
        public void TeleportMakesNoSoundAndRestartsTheStride()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0.9f, 0f, 0.18f, true, false, false);

            var cue = cadence.Advance(20f, 3f, _frame, true, false, false);

            Assert.That(cue, Is.EqualTo(FootstepCue.None));
            Assert.That(cadence.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
            Assert.That(cadence.Advance(0f, 0f, _frame, true, false, false), Is.EqualTo(FootstepCue.None), "teleporting onto the ground is not a landing");
        }

        [Test]
        public void TeleportIntoTheAirThenFallingStillLands()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0f, 0f, _frame, true, false, false);

            cadence.Advance(0f, 30f, _frame, false, false, false);
            cadence.Advance(0f, -8f * _frame, _frame, false, false, false);

            Assert.That(cadence.Advance(0f, 0f, _frame, true, false, false).Kind, Is.EqualTo(FootstepCueKind.Landing));
        }

        [Test]
        public void ZeroDeltaChangesNothing()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0f, 0f, _frame, true, false, false);
            cadence.Advance(0f, -8f * _frame, _frame, false, false, false);

            Assert.That(cadence.Advance(0f, 0f, 0f, true, false, false), Is.EqualTo(FootstepCue.None));
            Assert.That(cadence.Advance(0f, 0f, _frame, true, false, false).Kind, Is.EqualTo(FootstepCueKind.Landing));
        }

        [Test]
        public void ResetForgetsTheGroundAndTheStride()
        {
            var cadence = new FootstepCadence();
            cadence.Advance(0f, 0f, _frame, true, false, false);
            cadence.Advance(0f, -8f * _frame, _frame, false, false, false);

            cadence.Reset();

            Assert.That(cadence.Progress, Is.EqualTo(FootstepCadence.FirstStepProgress));
            Assert.That(cadence.Advance(0f, 0f, _frame, true, false, false), Is.EqualTo(FootstepCue.None));
        }

        [TestCase(5f, false)]
        [TestCase(5.4f, false)]
        [TestCase(5.6f, true)]
        [TestCase(8f, true)]
        [TestCase(0f, false)]
        public void InfersSprintingFromSpeed(float speed, bool expected)
        {
            Assert.That(FootstepCadence.IsSprintingBySpeed(speed, _walkSpeed), Is.EqualTo(expected));
        }

        [Test]
        public void CueRejectsInvalidValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCue((FootstepCueKind)99, 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCue(FootstepCueKind.Footstep, 1.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FootstepCue(FootstepCueKind.None, 0.5f));
            Assert.That(default(FootstepCue).HasSound, Is.False);
            Assert.That(new FootstepCue(FootstepCueKind.Landing, 0.5f).ToString(), Is.EqualTo("Landing volume=0.50"));
        }

        // seconds 秒、speed で接地したまま歩き、足音の数・最初の足音の時刻・最後の足音の音量を返す
        private static int Walk(FootstepCadence cadence, float speed, float seconds, bool crouching, bool sprinting, out float firstAt, out float volume)
        {
            var frames = (int)Math.Round(seconds / _frame);
            var steps = 0;
            firstAt = float.NaN;
            volume = 0f;
            for (var i = 1; i <= frames; i++)
            {
                var cue = cadence.Advance(speed * _frame, 0f, _frame, true, crouching, sprinting);
                if (cue.Kind != FootstepCueKind.Footstep)
                {
                    continue;
                }

                steps++;
                volume = cue.Volume;
                if (steps == 1)
                {
                    firstAt = i * _frame;
                }
            }

            return steps;
        }
    }
}

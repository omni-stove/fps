using System;
using System.Numerics;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class CharacterPoseTraversalTests
    {
        private readonly CharacterPose _pose = new CharacterPose(5f, 8f, 2.5f);

        [TestCase(-1f)]
        [TestCase(-0.25f)]
        [TestCase(0.5f)]
        [TestCase(1f)]
        public void LeanPassesTheAmountThrough(float lean)
        {
            var parameters = _pose.Evaluate(Input(lean: lean));

            Assert.That(parameters.Lean, Is.EqualTo(lean));
            Assert.That(parameters.Swimming, Is.False);
            Assert.That(parameters.WallRun, Is.EqualTo(CharacterPose.WallRunNone));
        }

        [Test]
        public void LeanKeepsCrouchAndAim()
        {
            var parameters = _pose.Evaluate(Input(lean: 1f, crouching: true, aiming: true));

            Assert.That(parameters.Lean, Is.EqualTo(1f));
            Assert.That(parameters.Crouch, Is.True);
            Assert.That(parameters.Aim, Is.True);
        }

        [Test]
        public void CoverDropsTheLean()
        {
            var parameters = _pose.Evaluate(Input(lean: -1f, cover: CoverHeight.High));

            Assert.That(parameters.Lean, Is.EqualTo(0f), "peeking from cover is shown by the cover pose");
            Assert.That(parameters.Cover, Is.EqualTo(CharacterPose.CoverHigh));
        }

        [Test]
        public void SwimmingRaisesSwimmingAndStrokesAtTheWalkScaleWhileAfloat()
        {
            var parameters = _pose.Evaluate(Input(speed: 2.5f, grounded: false, swimming: true, crouching: true, sliding: true, lean: 1f));

            Assert.That(parameters.Swimming, Is.True);
            Assert.That(parameters.Speed, Is.EqualTo(0.5f).Within(1e-5f), "2.5 m/s against a 5 m/s walk");
            Assert.That(parameters.Crouch, Is.False);
            Assert.That(parameters.Slide, Is.False);
            Assert.That(parameters.Lean, Is.EqualTo(0f));
        }

        [Test]
        public void SwimmingFasterThanWalkingStaysAtTheWalkValue()
        {
            Assert.That(_pose.Evaluate(Input(speed: 9f, grounded: false, swimming: true, sprinting: true)).Speed, Is.EqualTo(CharacterPose.WalkSpeedValue));
        }

        [TestCase(WallRunSide.Left, CharacterPose.WallRunLeft)]
        [TestCase(WallRunSide.Right, CharacterPose.WallRunRight)]
        public void WallRunSetsTheWallSideAndRunsInTheAir(WallRunSide side, int expected)
        {
            var parameters = _pose.Evaluate(Input(speed: 8f, grounded: false, wallRun: side, crouching: true, lean: -1f));

            Assert.That(parameters.WallRun, Is.EqualTo(expected));
            Assert.That(parameters.Speed, Is.EqualTo(CharacterPose.RunSpeedValue).Within(1e-5f), "the feet run along the wall even off the ground");
            Assert.That(parameters.Crouch, Is.False);
            Assert.That(parameters.Lean, Is.EqualTo(0f));
        }

        [Test]
        public void WallRunWinsOverSwimming()
        {
            var parameters = _pose.Evaluate(Input(grounded: false, wallRun: WallRunSide.Left, swimming: true));

            Assert.That(parameters.WallRun, Is.EqualTo(CharacterPose.WallRunLeft));
            Assert.That(parameters.Swimming, Is.False);
        }

        [Test]
        public void DeathDownedAndCoverWinOverTheNewPoses()
        {
            var dead = _pose.Evaluate(Input(lean: 1f, swimming: true, wallRun: WallRunSide.Right, dead: true));
            Assert.That(dead.Lean, Is.EqualTo(0f));
            Assert.That(dead.Swimming, Is.False);
            Assert.That(dead.WallRun, Is.EqualTo(CharacterPose.WallRunNone));

            var downed = _pose.Evaluate(Input(lean: 1f, swimming: true, wallRun: WallRunSide.Right, downed: true));
            Assert.That(downed.Swimming, Is.False);
            Assert.That(downed.WallRun, Is.EqualTo(CharacterPose.WallRunNone));

            var inCover = _pose.Evaluate(Input(swimming: true, wallRun: WallRunSide.Left, cover: CoverHeight.Low));
            Assert.That(inCover.Swimming, Is.False);
            Assert.That(inCover.WallRun, Is.EqualTo(CharacterPose.WallRunNone));
        }

        [Test]
        public void WithMotorTakesTheLeanSwimAndWallRunFromTheMotor()
        {
            var motor = new CharacterMotorState(
                Vector3.Zero,
                Vector3.Zero,
                false,
                PlayerStance.Standing,
                CharacterCoverState.Out,
                DodgeSnapshot.Ready,
                CharacterVaultState.None,
                CharacterProneState.None,
                CharacterLadderState.None,
                CharacterKnockbackState.None,
                new CharacterLeanState(-0.5f),
                new CharacterSwimState(true, 1f, false),
                new CharacterWallRunState(WallRunSide.Right, 0.2f, new Vector3(-1f, 0f, 0f)),
                0);
            var input = Input(speed: 3f, aiming: true);

            var withMotor = input.WithMotor(motor);

            Assert.That(withMotor.Lean, Is.EqualTo(-0.5f));
            Assert.That(withMotor.IsSwimming, Is.True);
            Assert.That(withMotor.WallRun, Is.EqualTo(WallRunSide.Right));
            Assert.That(withMotor.HorizontalSpeed, Is.EqualTo(3f));
            Assert.That(withMotor.IsAiming, Is.True);
            Assert.That(withMotor.WeaponCategory, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(Input().WithMotor(default), Is.EqualTo(Input()), "a still motor leaves the input as it was");
        }

        [Test]
        public void WithMotorRejectsALeanOutOfRange()
        {
            var motor = new CharacterMotorState(
                Vector3.Zero, Vector3.Zero, true, PlayerStance.Standing, CharacterCoverState.Out, DodgeSnapshot.Ready, CharacterVaultState.None,
                CharacterProneState.None, CharacterLadderState.None, CharacterKnockbackState.None, new CharacterLeanState(float.NaN),
                CharacterSwimState.None, CharacterWallRunState.None, 0);

            Assert.Throws<ArgumentOutOfRangeException>(() => Input().WithMotor(motor));
        }

        [TestCase(-1.01f)]
        [TestCase(1.01f)]
        [TestCase(float.NaN)]
        public void RejectsALeanOutOfRange(float lean)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(lean: lean));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, lean: lean));
        }

        [Test]
        public void RejectsAnUndefinedWallRun()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(wallRun: (WallRunSide)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterPose.WallRunValue((WallRunSide)9));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, wallRun: 3));
        }

        [Test]
        public void TheNewPosesTakePartInEqualityAndText()
        {
            Assert.That(Input(lean: 0.5f), Is.Not.EqualTo(Input(lean: -0.5f)));
            Assert.That(Input(swimming: true), Is.Not.EqualTo(Input()));
            Assert.That(Input(wallRun: WallRunSide.Left), Is.Not.EqualTo(Input(wallRun: WallRunSide.Right)));
            Assert.That(Input(lean: 0.5f).GetHashCode(), Is.EqualTo(Input(lean: 0.5f).GetHashCode()));
            Assert.That(Input(lean: 0.5f, swimming: true, wallRun: WallRunSide.Left).ToString(), Does.Contain("lean=0.50 swim=True wallRun=Left"));

            var leaning = new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, lean: 1f);
            var swimming = new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, swimming: true);
            var wallRunning = new CharacterPoseParameters(
                0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, wallRun: CharacterPose.WallRunRight);
            var still = new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon);
            Assert.That(leaning, Is.Not.EqualTo(still));
            Assert.That(swimming, Is.Not.EqualTo(still));
            Assert.That(wallRunning, Is.Not.EqualTo(still));
            Assert.That(wallRunning.ToString(), Does.Contain("Lean=0.00 Swimming=False WallRun=2"));
        }

        [Test]
        public void ParameterNamesAreDistinctFromTheOthers()
        {
            var names = new[]
            {
                CharacterPose.SpeedParameter,
                CharacterPose.CrouchParameter,
                CharacterPose.AimParameter,
                CharacterPose.CoverParameter,
                CharacterPose.DownedParameter,
                CharacterPose.DeadParameter,
                CharacterPose.WeaponCategoryParameter,
                CharacterPose.SlideParameter,
                CharacterPose.LeanParameter,
                CharacterPose.SwimmingParameter,
                CharacterPose.WallRunParameter,
            };

            Assert.That(names, Is.Unique);
        }

        private static CharacterPoseInput Input(
            float speed = 0f,
            bool grounded = true,
            bool crouching = false,
            bool sprinting = false,
            bool aiming = false,
            CoverHeight? cover = null,
            bool downed = false,
            bool dead = false,
            bool sliding = false,
            float lean = 0f,
            bool swimming = false,
            WallRunSide wallRun = WallRunSide.None)
        {
            return new CharacterPoseInput(
                speed, grounded, crouching, sprinting, aiming, cover, downed, dead, WeaponCategory.AssaultRifle, sliding, lean, swimming, wallRun);
        }
    }
}

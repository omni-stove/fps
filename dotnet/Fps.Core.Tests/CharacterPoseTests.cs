using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CharacterPoseTests
    {
        private const float _tolerance = 1e-5f;

        // PlayerController の既定値: 歩き 5、ダッシュ 8、しゃがみ 2.5 (m/s)。
        private static CharacterPose Pose()
        {
            return new CharacterPose(walkSpeed: 5f, sprintSpeed: 8f, crouchSpeed: 2.5f);
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
            WeaponCategory? weapon = WeaponCategory.AssaultRifle)
        {
            return new CharacterPoseInput(speed, grounded, crouching, sprinting, aiming, cover, downed, dead, weapon);
        }

        [Test]
        public void StandingStillIsIdle()
        {
            var parameters = Pose().Evaluate(Input());

            Assert.That(parameters, Is.EqualTo(new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, (int)WeaponCategory.AssaultRifle)));
        }

        [TestCase(0.05f, 0f)]
        [TestCase(2.5f, 0.5f)]
        [TestCase(5f, 1f)]
        public void WalkingScalesUpToOneAtWalkSpeed(float speed, float expected)
        {
            Assert.That(Pose().Evaluate(Input(speed)).Speed, Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void FasterThanWalkWithoutSprintStaysAtWalk()
        {
            // 回避で速くなっても、ダッシュしていなければ走る姿勢にしない。
            Assert.That(Pose().Evaluate(Input(9f)).Speed, Is.EqualTo(CharacterPose.WalkSpeedValue));
        }

        [TestCase(5f, 1f)]
        [TestCase(6.5f, 1.5f)]
        [TestCase(8f, 2f)]
        [TestCase(12f, 2f)]
        public void SprintingScalesFromWalkToRun(float speed, float expected)
        {
            Assert.That(Pose().Evaluate(Input(speed, sprinting: true)).Speed, Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void SprintingSlowerThanWalkUsesWalkScale()
        {
            Assert.That(Pose().Evaluate(Input(2.5f, sprinting: true)).Speed, Is.EqualTo(0.5f).Within(_tolerance));
        }

        [TestCase(0f, 0f)]
        [TestCase(1.25f, 0.5f)]
        [TestCase(2.5f, 1f)]
        [TestCase(4f, 1f)]
        public void CrouchingScalesByCrouchSpeedAndIgnoresSprint(float speed, float expected)
        {
            var parameters = Pose().Evaluate(Input(speed, crouching: true, sprinting: true));

            Assert.That(parameters.Crouch, Is.True);
            Assert.That(parameters.Speed, Is.EqualTo(expected).Within(_tolerance));
        }

        [Test]
        public void AirborneDoesNotMoveTheLegs()
        {
            var parameters = Pose().Evaluate(Input(8f, grounded: false, sprinting: true));

            Assert.That(parameters.Speed, Is.EqualTo(CharacterPose.IdleSpeedValue));
        }

        [Test]
        public void AimingKeepsSpeedAndCrouch()
        {
            var standing = Pose().Evaluate(Input(5f, aiming: true));
            var crouched = Pose().Evaluate(Input(2.5f, crouching: true, aiming: true));

            Assert.That(standing.Aim, Is.True);
            Assert.That(standing.Crouch, Is.False);
            Assert.That(standing.Speed, Is.EqualTo(1f).Within(_tolerance));
            Assert.That(crouched.Aim, Is.True);
            Assert.That(crouched.Crouch, Is.True);
        }

        [TestCase(CoverHeight.Low, CharacterPose.CoverLow)]
        [TestCase(CoverHeight.High, CharacterPose.CoverHigh)]
        public void CoverSetsTheWallHeightAndDropsCrouch(CoverHeight height, int expected)
        {
            var parameters = Pose().Evaluate(Input(crouching: true, cover: height));

            Assert.That(parameters.Cover, Is.EqualTo(expected));
            Assert.That(parameters.Crouch, Is.False);
            Assert.That(parameters.Aim, Is.False);
        }

        [Test]
        public void AimingFromCoverKeepsCoverAndAim()
        {
            var parameters = Pose().Evaluate(Input(cover: CoverHeight.Low, aiming: true));

            Assert.That(parameters.Cover, Is.EqualTo(CharacterPose.CoverLow));
            Assert.That(parameters.Aim, Is.True);
        }

        [Test]
        public void DownedOverridesEverythingButWeapon()
        {
            var parameters = Pose().Evaluate(
                Input(8f, crouching: true, sprinting: true, aiming: true, cover: CoverHeight.High, downed: true, weapon: WeaponCategory.Handgun));

            Assert.That(parameters, Is.EqualTo(new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, true, false, (int)WeaponCategory.Handgun)));
        }

        [Test]
        public void DeadOverridesDowned()
        {
            var parameters = Pose().Evaluate(
                Input(8f, crouching: true, sprinting: true, aiming: true, cover: CoverHeight.Low, downed: true, dead: true, weapon: WeaponCategory.Shotgun));

            Assert.That(parameters, Is.EqualTo(new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, true, (int)WeaponCategory.Shotgun)));
        }

        [Test]
        public void NoWeaponIsMinusOne()
        {
            Assert.That(Pose().Evaluate(Input(weapon: null)).WeaponCategory, Is.EqualTo(CharacterPose.NoWeapon));
        }

        [Test]
        public void EveryWeaponCategoryPassesThrough()
        {
            foreach (WeaponCategory category in Enum.GetValues(typeof(WeaponCategory)))
            {
                Assert.That(Pose().Evaluate(Input(weapon: category)).WeaponCategory, Is.EqualTo((int)category), category.ToString());
                Assert.That((int)category, Is.Not.EqualTo(CharacterPose.NoWeapon));
            }
        }

        [Test]
        public void DefaultInputIsAirborneIdleWithoutWeapon()
        {
            var parameters = Pose().Evaluate(default);

            Assert.That(parameters, Is.EqualTo(new CharacterPoseParameters(0f, false, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon)));
        }

        [Test]
        public void FromMotorUsesTheMotorSpeeds()
        {
            var pose = CharacterPose.FromMotor(new MotorSpec(4f, 7f, 1f, 20f, 0.3f, 2f));

            Assert.That(pose.WalkSpeed, Is.EqualTo(4f));
            Assert.That(pose.SprintSpeed, Is.EqualTo(7f));
            Assert.That(pose.CrouchSpeed, Is.EqualTo(2f));
            Assert.Throws<ArgumentNullException>(() => CharacterPose.FromMotor(null));
        }

        [Test]
        public void ParameterNamesAreDistinct()
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
            };

            Assert.That(names, Is.Unique);
        }

        [TestCase(0.1f, 8f, 2.5f)]
        [TestCase(float.NaN, 8f, 2.5f)]
        [TestCase(float.PositiveInfinity, 8f, 2.5f)]
        [TestCase(5f, 5f, 2.5f)]
        [TestCase(5f, 4f, 2.5f)]
        [TestCase(5f, float.PositiveInfinity, 2.5f)]
        [TestCase(5f, 8f, 0f)]
        [TestCase(5f, 8f, float.NaN)]
        public void RejectsInvalidSpeeds(float walk, float sprint, float crouch)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterPose(walk, sprint, crouch));
        }

        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidInputSpeed(float speed)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(speed));
            Assert.Throws<ArgumentOutOfRangeException>(() => Pose().SpeedValue(speed, false, false));
        }

        [Test]
        public void RejectsUndefinedEnums()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(cover: (CoverHeight)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => Input(weapon: (WeaponCategory)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => CharacterPose.CoverValue((CoverHeight)99));
        }

        [TestCase(-0.1f, CharacterPose.CoverNone, 0)]
        [TestCase(2.1f, CharacterPose.CoverNone, 0)]
        [TestCase(float.NaN, CharacterPose.CoverNone, 0)]
        [TestCase(1f, 3, 0)]
        [TestCase(1f, -1, 0)]
        [TestCase(1f, CharacterPose.CoverNone, -2)]
        [TestCase(1f, CharacterPose.CoverNone, 99)]
        public void ParametersRejectOutOfRangeValues(float speed, int cover, int weapon)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new CharacterPoseParameters(speed, false, false, cover, false, false, weapon));
        }

        [Test]
        public void InputsWithTheSameValuesAreEqual()
        {
            var a = Input(3f, aiming: true, cover: CoverHeight.High);
            var b = Input(3f, aiming: true, cover: CoverHeight.High);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(Input(3f, aiming: true, cover: CoverHeight.Low)));
            Assert.That(a.IsInCover, Is.True);
        }
    }
}

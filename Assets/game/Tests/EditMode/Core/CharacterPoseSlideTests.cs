using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CharacterPoseSlideTests
    {
        private readonly CharacterPose _pose = new CharacterPose(5f, 8f, 2.5f);

        [Test]
        public void SlidingRaisesSlideAndCrouch()
        {
            var parameters = _pose.Evaluate(Input(sliding: true));

            Assert.That(parameters.Slide, Is.True);
            Assert.That(parameters.Crouch, Is.True, "a rig without a slide pose shows the crouch");
            Assert.That(parameters.Speed, Is.EqualTo(CharacterPose.WalkSpeedValue), "fast but crouched, so it does not run");
        }

        [Test]
        public void NotSlidingLeavesSlideDown()
        {
            var parameters = _pose.Evaluate(Input(sliding: false));

            Assert.That(parameters.Slide, Is.False);
            Assert.That(parameters, Is.EqualTo(_pose.Evaluate(new CharacterPoseInput(11f, true, true, false, false, null, false, false, WeaponCategory.AssaultRifle))));
        }

        [Test]
        public void DeathDownedAndCoverWinOverTheSlide()
        {
            Assert.That(_pose.Evaluate(Input(sliding: true, dead: true)).Slide, Is.False);
            Assert.That(_pose.Evaluate(Input(sliding: true, downed: true)).Slide, Is.False);

            var inCover = _pose.Evaluate(Input(sliding: true, cover: CoverHeight.Low));
            Assert.That(inCover.Slide, Is.False);
            Assert.That(inCover.Crouch, Is.False);
        }

        [Test]
        public void TheSlideTakesPartInEqualityAndText()
        {
            var sliding = new CharacterPoseParameters(1f, true, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon, true);
            var crouching = new CharacterPoseParameters(1f, true, false, CharacterPose.CoverNone, false, false, CharacterPose.NoWeapon);

            Assert.That(sliding, Is.Not.EqualTo(crouching));
            Assert.That(sliding.ToString(), Does.EndWith("Slide=True"));
            Assert.That(Input(sliding: true), Is.Not.EqualTo(Input(sliding: false)));
            Assert.That(Input(sliding: true).ToString(), Does.EndWith("slide=True"));
        }

        private static CharacterPoseInput Input(bool sliding, bool dead = false, bool downed = false, CoverHeight? cover = null)
        {
            return new CharacterPoseInput(11f, true, true, false, false, cover, downed, dead, WeaponCategory.AssaultRifle, sliding);
        }
    }
}

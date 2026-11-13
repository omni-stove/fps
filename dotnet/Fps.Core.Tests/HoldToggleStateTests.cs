using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class HoldToggleStateTests
    {
        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void StartsReleased(HoldMode mode)
        {
            var state = new HoldToggleState(mode);

            Assert.That(state.Mode, Is.EqualTo(mode));
            Assert.That(state.IsActive, Is.False);
        }

        [Test]
        public void HoldIsActiveOnlyWhilePressed()
        {
            var state = new HoldToggleState(HoldMode.Hold);

            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(true, true), Is.True);
            Assert.That(state.Update(true, false), Is.True);
            Assert.That(state.IsActive, Is.True);
            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(true, true), Is.True);
        }

        [Test]
        public void HoldIgnoresAPressReleasedInTheSameFrame()
        {
            var state = new HoldToggleState(HoldMode.Hold);

            Assert.That(state.Update(false, true), Is.False);
        }

        [Test]
        public void ToggleFlipsOnEachPress()
        {
            var state = new HoldToggleState(HoldMode.Toggle);

            Assert.That(state.Update(true, true), Is.True);
            Assert.That(state.Update(true, false), Is.True, "holding does not flip again");
            Assert.That(state.Update(false, false), Is.True, "letting go keeps it on");
            Assert.That(state.Update(true, true), Is.False);
            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(false, true), Is.True, "a tap within one frame still counts");
        }

        [Test]
        public void ReleaseTurnsHoldOffUntilPressedAgain()
        {
            var state = new HoldToggleState(HoldMode.Hold);
            state.Update(true, true);

            state.Release();

            Assert.That(state.IsActive, Is.False);
            Assert.That(state.Update(true, false), Is.False, "keeping the button down does not reactivate it");
            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(true, true), Is.True);
        }

        [Test]
        public void ReleaseTurnsToggleOffAndTheNextPressTurnsItOn()
        {
            var state = new HoldToggleState(HoldMode.Toggle);
            state.Update(true, true);
            state.Update(false, false);

            state.Release();

            Assert.That(state.IsActive, Is.False);
            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(true, true), Is.True);
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void ReleaseWhenAlreadyReleasedDoesNothing(HoldMode mode)
        {
            var state = new HoldToggleState(mode);

            state.Release();

            Assert.That(state.IsActive, Is.False);
            Assert.That(state.Update(true, true), Is.True);
        }

        [Test]
        public void SwitchingFromToggleToHoldStartsReleased()
        {
            var state = new HoldToggleState(HoldMode.Toggle);
            state.Update(true, true);

            state.Mode = HoldMode.Hold;

            Assert.That(state.Mode, Is.EqualTo(HoldMode.Hold));
            Assert.That(state.IsActive, Is.False);
            Assert.That(state.Update(true, false), Is.False, "the button held across the switch does not count");
            Assert.That(state.Update(true, true), Is.True);
            Assert.That(state.Update(false, false), Is.False);
        }

        [Test]
        public void SwitchingFromHoldToToggleStartsReleased()
        {
            var state = new HoldToggleState(HoldMode.Hold);
            state.Update(true, true);

            state.Mode = HoldMode.Toggle;

            Assert.That(state.IsActive, Is.False);
            Assert.That(state.Update(false, false), Is.False);
            Assert.That(state.Update(true, true), Is.True);
            Assert.That(state.Update(false, false), Is.True);
        }

        [TestCase(HoldMode.Hold)]
        [TestCase(HoldMode.Toggle)]
        public void SettingTheSameModeKeepsTheState(HoldMode mode)
        {
            var state = new HoldToggleState(mode);
            state.Update(true, true);

            state.Mode = mode;

            Assert.That(state.IsActive, Is.True);
        }

        [Test]
        public void RejectsUnknownModes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new HoldToggleState((HoldMode)2));

            var state = new HoldToggleState(HoldMode.Toggle);
            state.Update(true, true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Mode = (HoldMode)(-1));
            Assert.That(state.Mode, Is.EqualTo(HoldMode.Toggle));
            Assert.That(state.IsActive, Is.True, "a rejected mode leaves the state alone");
        }
    }
}

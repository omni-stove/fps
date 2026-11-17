using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LeanInputTests
    {
        [Test]
        public void StartsUprightInHoldMode()
        {
            var lean = new LeanInput();

            Assert.That(lean.Mode, Is.EqualTo(HoldMode.Hold));
            Assert.That(lean.Lean, Is.EqualTo(0));
        }

        [TestCase(false, false, 0)]
        [TestCase(true, false, -1)]
        [TestCase(false, true, 1)]
        [TestCase(true, true, 0)]
        public void CombinePicksTheSideThatIsAlone(bool left, bool right, int expected)
        {
            Assert.That(LeanInput.Combine(left, right), Is.EqualTo(expected));
        }

        [Test]
        public void HoldLeansOnlyWhilePressed()
        {
            var lean = new LeanInput(HoldMode.Hold);

            Assert.That(lean.Update(true, true, false, false), Is.EqualTo(-1));
            Assert.That(lean.Update(true, false, false, false), Is.EqualTo(-1));
            Assert.That(lean.Update(false, false, false, false), Is.EqualTo(0));
            Assert.That(lean.Update(false, false, true, true), Is.EqualTo(1));
            Assert.That(lean.Update(false, false, false, false), Is.EqualTo(0));
        }

        [Test]
        public void HoldingBothIsUpright()
        {
            var lean = new LeanInput(HoldMode.Hold);
            lean.Update(true, true, false, false);

            Assert.That(lean.Update(true, false, true, true), Is.EqualTo(0));
            Assert.That(lean.IsLeftActive, Is.True);
            Assert.That(lean.IsRightActive, Is.True);
            Assert.That(lean.Update(false, false, true, false), Is.EqualTo(1), "letting go of the left leaves the right");
        }

        [Test]
        public void PressingBothAtOnceIsUpright()
        {
            Assert.That(new LeanInput(HoldMode.Hold).Update(true, true, true, true), Is.EqualTo(0));
            Assert.That(new LeanInput(HoldMode.Toggle).Update(true, true, true, true), Is.EqualTo(0));
        }

        [Test]
        public void ToggleStaysLeaningAfterLettingGoAndStandsOnTheNextPress()
        {
            var lean = new LeanInput(HoldMode.Toggle);

            Assert.That(lean.Update(false, false, true, true), Is.EqualTo(1));
            Assert.That(lean.Update(false, false, false, false), Is.EqualTo(1), "letting go keeps leaning");
            Assert.That(lean.Update(false, false, true, true), Is.EqualTo(0));
            Assert.That(lean.Update(false, false, false, false), Is.EqualTo(0));
        }

        [Test]
        public void ToggleSwitchesStraightToTheOtherSide()
        {
            var lean = new LeanInput(HoldMode.Toggle);
            lean.Update(true, true, false, false);
            lean.Update(false, false, false, false);

            Assert.That(lean.Update(false, false, true, true), Is.EqualTo(1), "the right replaces the left instead of cancelling it");
            Assert.That(lean.IsLeftActive, Is.False);
            Assert.That(lean.Update(true, true, false, false), Is.EqualTo(-1));
            Assert.That(lean.IsRightActive, Is.False);
        }

        [Test]
        public void ChangingTheModeStandsUp()
        {
            var lean = new LeanInput(HoldMode.Toggle);
            lean.Update(false, false, true, true);

            lean.Mode = HoldMode.Hold;

            Assert.That(lean.Lean, Is.EqualTo(0));
            Assert.That(lean.Update(false, false, true, false), Is.EqualTo(0), "hold needs a fresh press after the change");
        }

        [Test]
        public void SettingTheSameModeKeepsLeaning()
        {
            var lean = new LeanInput(HoldMode.Toggle);
            lean.Update(true, true, false, false);

            lean.Mode = HoldMode.Toggle;

            Assert.That(lean.Lean, Is.EqualTo(-1));
        }

        [Test]
        public void ReleaseStandsUpUntilPressedAgain()
        {
            var lean = new LeanInput(HoldMode.Hold);
            lean.Update(false, false, true, true);

            lean.Release();

            Assert.That(lean.Lean, Is.EqualTo(0));
            Assert.That(lean.Update(false, false, true, false), Is.EqualTo(0), "still holding is not a new press");
            Assert.That(lean.Update(false, false, true, true), Is.EqualTo(1));
        }

        [Test]
        public void RejectsAnUnknownMode()
        {
            var lean = new LeanInput(HoldMode.Toggle);

            Assert.Throws<ArgumentOutOfRangeException>(() => new LeanInput((HoldMode)9));
            Assert.Throws<ArgumentOutOfRangeException>(() => lean.Mode = (HoldMode)9);
            Assert.That(lean.Mode, Is.EqualTo(HoldMode.Toggle));
        }
    }
}

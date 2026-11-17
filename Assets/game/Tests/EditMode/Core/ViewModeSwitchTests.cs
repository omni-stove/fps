using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ViewModeSwitchTests
    {
        [Test]
        public void OtherFlipsBetweenFirstAndThirdPerson()
        {
            Assert.That(ViewModeSwitch.Other(ViewMode.FirstPerson), Is.EqualTo(ViewMode.ThirdPerson));
            Assert.That(ViewModeSwitch.Other(ViewMode.ThirdPerson), Is.EqualTo(ViewMode.FirstPerson));
        }

        [Test]
        public void OtherRejectsUndefinedModes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewModeSwitch.Other((ViewMode)99));
        }

        [Test]
        public void SwitchLogNamesBothModesWithTheTag()
        {
            Assert.That(
                ViewModeSwitch.FormatSwitchLog(ViewMode.FirstPerson, ViewMode.ThirdPerson),
                Is.EqualTo("[ViewSwitch] view: FirstPerson -> ThirdPerson"));
        }

        [Test]
        public void RefusalLogsNameTheReason()
        {
            Assert.That(
                ViewModeSwitch.FormatRefusalLog(ViewModeSwitch.Refusal.NoLocalView),
                Is.EqualTo("[ViewSwitch] refused: no local player view in this scene"));
            Assert.That(
                ViewModeSwitch.FormatRefusalLog(ViewModeSwitch.Refusal.ViewBorrowed),
                Is.EqualTo("[ViewSwitch] refused: the view is borrowed by the death or vehicle camera"));
        }

        [Test]
        public void RefusalLogRejectsNone()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ViewModeSwitch.FormatRefusalLog(ViewModeSwitch.Refusal.None));
        }
    }
}

using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class AimBlockSettingsTests
    {
        [Test]
        public void DefaultIsEnabledWithAThirtyCentimetreMargin()
        {
            Assert.That(AimBlockSettings.Default.Enabled, Is.True);
            Assert.That(AimBlockSettings.Default.Margin, Is.EqualTo(0.3f));
        }

        [Test]
        public void KeepsTheGivenValues()
        {
            var settings = new AimBlockSettings(false, 1.25f);

            Assert.That(settings.Enabled, Is.False);
            Assert.That(settings.Margin, Is.EqualTo(1.25f));
        }

        [Test]
        public void AllowsZeroMargin()
        {
            Assert.That(new AimBlockSettings(true, 0f).Margin, Is.Zero);
        }

        [Test]
        public void RejectsNegativeOrNonFiniteMargin()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new AimBlockSettings(true, -0.01f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AimBlockSettings(true, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new AimBlockSettings(true, float.PositiveInfinity));
        }
    }
}

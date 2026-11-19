using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class VisionLogTests
    {
        [Test]
        public void ChangesNameTheModeTheSourceAndWhatCameBefore()
        {
            Assert.That(
                VisionLog.Changed(VisionStatus.None, new VisionStatus(VisionMode.Night, VisionSource.Goggles)),
                Is.EqualTo("[Vision] Night/Goggles was=none"));
            Assert.That(
                VisionLog.Changed(new VisionStatus(VisionMode.Night, VisionSource.Goggles), new VisionStatus(VisionMode.Thermal, VisionSource.Scope)),
                Is.EqualTo("[Vision] Thermal/Scope was=Night/Goggles"));
            Assert.That(
                VisionLog.Changed(new VisionStatus(VisionMode.Thermal, VisionSource.Scope), VisionStatus.None),
                Is.EqualTo("[Vision] none was=Thermal/Scope"));
        }

        [Test]
        public void GogglesLinesNameTheOwnerAndTheMode()
        {
            Assert.That(VisionLog.Goggles("player", VisionMode.Thermal, true), Is.EqualTo("[Vision] goggles owner=player mode=Thermal on"));
            Assert.That(VisionLog.Goggles(null, VisionMode.Night, false), Is.EqualTo("[Vision] goggles owner=- mode=Night off"));
            Assert.That(VisionLog.GogglesOffOnDeath("player"), Is.EqualTo("[Vision] goggles owner=player off reason=died"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VisionLog.Goggles("player", (VisionMode)99, true));
        }

        [Test]
        public void HighlightLinesCountTheBodies()
        {
            Assert.That(VisionLog.Highlighted(2, 3, true), Is.EqualTo("[Vision] thermal highlighted added=2 total=3 material=code"));
            Assert.That(VisionLog.Highlighted(1, 1, false), Is.EqualTo("[Vision] thermal highlighted added=1 total=1 material=catalog"));
            Assert.That(VisionLog.Restored(4, 0), Is.EqualTo("[Vision] thermal restored=4 total=0"));
            Assert.Throws<ArgumentOutOfRangeException>(() => VisionLog.Highlighted(-1, 0, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => VisionLog.Restored(0, -1));
        }
    }
}

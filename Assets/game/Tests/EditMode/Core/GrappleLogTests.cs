using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class GrappleLogTests
    {
        [Test]
        public void LinesStartWithTheTagAndUseInvariantNumbers()
        {
            Assert.That(GrappleLog.Attached("Wall", 12.3456f, true), Is.EqualTo("[Grapple] attached surface=Wall distance=12.35 edge=true"));
            Assert.That(GrappleLog.Attached(null, 3f, false), Is.EqualTo("[Grapple] attached surface=- distance=3.00 edge=false"));
            Assert.That(GrappleLog.Miss(GrappleMiss.OutOfRange, null, null), Is.EqualTo("[Grapple] miss reason=OutOfRange surface=- distance=-"));
            Assert.That(
                GrappleLog.Miss(GrappleMiss.NotGrabbable, "Glass", 8f),
                Is.EqualTo("[Grapple] miss reason=NotGrabbable surface=Glass distance=8.00"));
            Assert.That(GrappleLog.Arrived(0.8261f, true), Is.EqualTo("[Grapple] arrived seconds=0.83 wall=true"));
            Assert.That(GrappleLog.Released(GrappleEnd.Jumped, 14.2f), Is.EqualTo("[Grapple] released reason=Jumped speed=14.20"));
            Assert.That(GrappleLog.Climb(true), Is.EqualTo("[Grapple] climb started=true"));
            Assert.That(GrappleLog.Climb(false), Is.EqualTo("[Grapple] climb started=false"));
        }

        [Test]
        public void ReasonsMustBeGiven()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GrappleLog.Miss(GrappleMiss.None, null, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrappleLog.Released(GrappleEnd.None, 0f));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrappleLog.Released(GrappleEnd.Arrived, 0f));
        }
    }
}

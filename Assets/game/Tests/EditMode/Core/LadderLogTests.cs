using System;
using System.Numerics;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LadderLogTests
    {
        [Test]
        public void GrabAndLeaveLinesNameTheLadderAndWhy()
        {
            Assert.That(LadderLog.Grab("Ladder", LadderEntry.Front, 0f), Is.EqualTo("[Ladder] grab ladder=Ladder entry=Front height=0.00"));
            Assert.That(LadderLog.Grab(null, LadderEntry.Top, 2.7f), Is.EqualTo("[Ladder] grab ladder=- entry=Top height=2.70"));
            Assert.That(LadderLog.Leave("Ladder", LadderOutcome.ExitTop, 3f), Is.EqualTo("[Ladder] leave ladder=Ladder reason=ExitTop height=3.00"));
            Assert.That(LadderLog.Leave("Ladder", LadderOutcome.ExitBottom, 0f), Does.StartWith(LadderLog.LeavePrefixFor("Ladder", LadderOutcome.ExitBottom)));
            Assert.That(LadderLog.Leave("Ladder", LadderOutcome.ExitTop, 3f), Does.Not.StartWith(LadderLog.LeavePrefixFor("Ladder", LadderOutcome.ExitBottom)));
            Assert.Throws<ArgumentOutOfRangeException>(() => LadderLog.Leave("Ladder", LadderOutcome.Climbing, 1f));
        }

        [Test]
        public void TheLinkLineHasBothEnds()
        {
            Assert.That(
                LadderLog.Link("Ladder", new Vector3(0f, 0f, -0.8f), new Vector3(0f, 3f, 0.8f), 1),
                Is.EqualTo("[Ladder] link ladder=Ladder from=(0.00, 0.00, -0.80) to=(0.00, 3.00, 0.80) agentTypes=1"));
        }

        [Test]
        public void TheSpecKeepsItsValuesAndRejectsBadOnes()
        {
            var spec = new LadderSpec(3f, 0.5f, 2f, 1f);

            Assert.That(spec.ClimbSpeed, Is.EqualTo(3f));
            Assert.That(spec.GrabDistance, Is.EqualTo(0.5f));
            Assert.That(spec.JumpOffSpeed, Is.EqualTo(2f));
            Assert.That(spec.RegrabSeconds, Is.EqualTo(1f));
            Assert.That(LadderSpec.Default.ClimbSpeed, Is.EqualTo(LadderSpec.DefaultClimbSpeed));
            Assert.That(new LadderSpec(1f, 0f, 0f, 0f).RegrabSeconds, Is.Zero, "zero is allowed except for the climb speed");

            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpec(0f, 0.5f, 2f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpec(float.PositiveInfinity, 0.5f, 2f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpec(3f, -0.1f, 2f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpec(3f, 0.5f, float.NaN, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new LadderSpec(3f, 0.5f, 2f, -1f));
        }
    }
}

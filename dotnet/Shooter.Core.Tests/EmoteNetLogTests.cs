using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class EmoteNetLogTests
    {
        [Test]
        public void TheNetLinesNameThePlayerTheIdAndTheReason()
        {
            Assert.That(EmoteLog.NetRequested("wave"), Is.EqualTo("[Emote] net requested id=wave"));
            Assert.That(EmoteLog.NetStopRequested(), Is.EqualTo("[Emote] net stop requested"));
            Assert.That(EmoteLog.NetNotSent("wave", "RuleOff"), Is.EqualTo("[Emote] net not sent id=wave reason=RuleOff"));
            Assert.That(EmoteLog.NetPlayed(3, "dance", true), Is.EqualTo("[Emote] net played player=3 id=dance animation=yes"));
            Assert.That(EmoteLog.NetPlayed(3, "dance", false), Is.EqualTo("[Emote] net played player=3 id=dance animation=none"));
            Assert.That(EmoteLog.NetStopped(3, "dance", EmoteStopReason.Moved), Is.EqualTo("[Emote] net stopped player=3 id=dance reason=Moved"));
            Assert.That(EmoteLog.NetStopped(3, null, EmoteStopReason.Finished), Is.EqualTo("[Emote] net stopped player=3 id=- reason=Finished"));
            Assert.That(
                EmoteLog.NetNoBody(3, "dance"),
                Is.EqualTo("[Emote] net no body player=3 id=dance; the emote is not shown (the player is out of view)"));
        }

        [Test]
        public void TheMoveToleranceIsShared()
        {
            Assert.That(EmoteSpec.MoveToleranceMeters, Is.EqualTo(0.05f));
        }

        [Test]
        public void TooSoonIsTheLastRefusal()
        {
            Assert.That((int)EmoteRefusal.TooSoon, Is.EqualTo(6));
            Assert.That((int)EmoteRefusal.Riding, Is.EqualTo(5));
        }
    }
}

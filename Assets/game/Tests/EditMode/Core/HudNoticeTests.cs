using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class HudNoticeTests
    {
        [Test]
        public void DefaultIsNone()
        {
            Assert.That(default(HudNotice).IsNone, Is.True);
            Assert.That(HudNotice.None, Is.EqualTo(default(HudNotice)));
            Assert.That(HudNotice.CheckpointSaved.IsNone, Is.False);
        }

        [Test]
        public void NoticesAreEqualByKindSecondsAndPercent()
        {
            var three = HudNotice.Respawn(3);
            var alsoThree = HudNotice.Respawn(3);
            Assert.That(three, Is.EqualTo(alsoThree));
            Assert.That(three.GetHashCode(), Is.EqualTo(alsoThree.GetHashCode()));
            Assert.That(HudNotice.Respawn(3), Is.Not.EqualTo(HudNotice.Respawn(2)));
            Assert.That(HudNotice.Respawn(3), Is.Not.EqualTo(HudNotice.NextMatch(3)));
            Assert.That(HudNotice.Downed(5, 40), Is.Not.EqualTo(HudNotice.Downed(5, 0)));
        }

        [Test]
        public void ForRespawnFollowsTheRespawnMode()
        {
            Assert.That(HudNotice.ForRespawn(RespawnMode.Individual, 4), Is.EqualTo(HudNotice.Respawn(4)));
            Assert.That(HudNotice.ForRespawn(RespawnMode.Wave, 7), Is.EqualTo(HudNotice.Respawn(7)));
            Assert.That(HudNotice.ForRespawn(RespawnMode.None, 3), Is.EqualTo(HudNotice.NoRespawnUntilNextMatch), "None ignores the seconds");
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.ForRespawn((RespawnMode)99, 0));
        }

        [Test]
        public void RejectsNegativeSecondsAndPercentOutOfRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.CheckpointRestart(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.NextMatch(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.IdleWarning(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.SpawnProtected(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.ForRespawn(RespawnMode.Wave, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.Downed(-1, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.Downed(1, 101));
            Assert.Throws<ArgumentOutOfRangeException>(() => HudNotice.Downed(1, -1));
        }
    }
}

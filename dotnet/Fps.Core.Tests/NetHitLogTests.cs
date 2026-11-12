using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class NetHitLogTests
    {
        [Test]
        public void FormatsHit()
        {
            var line = NetHitLog.Hit(0, 1, 6.4f, HitZone.Body, 25, 75, DamageOutcome.Damaged);

            Assert.That(line, Is.EqualTo("[Net] hit attacker=0 victim=1 rewindTicks=6.40 zone=Body dmg=25 hp=75 outcome=Damaged"));
        }

        [Test]
        public void FormatsKillingHeadshot()
        {
            var line = NetHitLog.Hit(3, 2, 0f, HitZone.Head, 50, 0, DamageOutcome.Killed);

            Assert.That(line, Is.EqualTo("[Net] hit attacker=3 victim=2 rewindTicks=0.00 zone=Head dmg=50 hp=0 outcome=Killed"));
        }

        [Test]
        public void FormatsRejectedShot()
        {
            Assert.That(
                NetHitLog.Rejected(1, ShotRejection.OriginTooFar),
                Is.EqualTo("[Net] rejected shot attacker=1 reason=OriginTooFar"));
        }

        [Test]
        public void RejectsUnknownEnums()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => NetHitLog.Hit(0, 1, 0f, (HitZone)99, 1, 1, DamageOutcome.Damaged));
            Assert.Throws<ArgumentOutOfRangeException>(() => NetHitLog.Hit(0, 1, 0f, HitZone.Body, 1, 1, (DamageOutcome)99));
        }
    }
}

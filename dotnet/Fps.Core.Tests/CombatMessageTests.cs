using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class CombatMessageTests
    {
        [Test]
        public void HitConfirmKeepsEveryGivenValue()
        {
            var hit = new HitConfirm(50, HitZone.Head, true, false);

            Assert.That(hit.Damage, Is.EqualTo(50));
            Assert.That(hit.Zone, Is.EqualTo(HitZone.Head));
            Assert.That(hit.Killed, Is.True);
            Assert.That(hit.Downed, Is.False);
        }

        [Test]
        public void HitConfirmAcceptsZeroDamageAndADown()
        {
            var hit = new HitConfirm(0, HitZone.Limb, false, true);

            Assert.That(hit.Damage, Is.EqualTo(0));
            Assert.That(hit.Downed, Is.True);
        }

        [Test]
        public void HitConfirmRejectsNegativeDamage()
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new HitConfirm(-1, HitZone.Body, false, false));
            Assert.That(ex.ParamName, Is.EqualTo("damage"));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void HitConfirmRejectsAnUndefinedZone(int value)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new HitConfirm(10, (HitZone)value, false, false));
            Assert.That(ex.ParamName, Is.EqualTo("zone"));
        }

        [Test]
        public void HitConfirmRejectsKillingAndDowningAtOnce()
        {
            var ex = Assert.Throws<ArgumentException>(() => new HitConfirm(10, HitZone.Body, true, true));
            Assert.That(ex.ParamName, Is.EqualTo("downed"));
        }

        [Test]
        public void HitConfirmsWithTheSameValuesAreEqual()
        {
            var a = new HitConfirm(25, HitZone.Body, false, true);
            var b = new HitConfirm(25, HitZone.Body, false, true);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(new HitConfirm(26, HitZone.Body, false, true)));
            Assert.That(a, Is.Not.EqualTo(new HitConfirm(25, HitZone.Head, false, true)));
            Assert.That(a, Is.Not.EqualTo(new HitConfirm(25, HitZone.Body, false, false)));
            Assert.That(a, Is.Not.EqualTo(new HitConfirm(25, HitZone.Body, true, false)));
        }

        [Test]
        public void KillFeedEntryKeepsEveryGivenValue()
        {
            var entry = new KillFeedEntry(1, 2, "6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83", true, false);

            Assert.That(entry.KillerId, Is.EqualTo(1));
            Assert.That(entry.VictimId, Is.EqualTo(2));
            Assert.That(entry.WeaponId, Is.EqualTo("6c1f3b2a-8d4e-4f7a-9b0c-2e5d7a1f4c83"));
            Assert.That(entry.Headshot, Is.True);
            Assert.That(entry.Friendly, Is.False);
            Assert.That(entry.IsSuicide, Is.False);
        }

        [Test]
        public void KillFeedEntryTreatsANullWeaponAsEmpty()
        {
            var entry = new KillFeedEntry(3, 3, null, false, false);

            Assert.That(entry.WeaponId, Is.Empty);
            Assert.That(entry.IsSuicide, Is.True);
            Assert.That(entry, Is.EqualTo(new KillFeedEntry(3, 3, string.Empty, false, false)));
        }

        [Test]
        public void DefaultKillFeedEntryHasAnEmptyWeapon()
        {
            Assert.That(default(KillFeedEntry).WeaponId, Is.Empty);
        }

        [Test]
        public void KillFeedEntriesWithTheSameValuesAreEqual()
        {
            var a = new KillFeedEntry(1, 2, "w", false, true);
            var b = new KillFeedEntry(1, 2, "w", false, true);

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(new KillFeedEntry(9, 2, "w", false, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedEntry(1, 9, "w", false, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedEntry(1, 2, "x", false, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedEntry(1, 2, "w", true, true)));
            Assert.That(a, Is.Not.EqualTo(new KillFeedEntry(1, 2, "w", false, false)));
        }
    }
}

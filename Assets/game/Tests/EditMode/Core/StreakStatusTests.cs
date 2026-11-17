using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class StreakStatusTests
    {
        [Test]
        public void ASlotKeepsItsValuesAndTurnsAMissingKeyIntoEmpty()
        {
            var slot = new StreakSlot("uav", null, 3, true);

            Assert.That(slot.RewardId, Is.EqualTo("uav"));
            Assert.That(slot.DisplayNameKey, Is.EqualTo(string.Empty));
            Assert.That(slot.KillsRequired, Is.EqualTo(3));
            Assert.That(slot.Ready, Is.True);
            Assert.That(slot, Is.EqualTo(new StreakSlot("uav", string.Empty, 3, true)));
            Assert.That(slot == new StreakSlot("uav", string.Empty, 3, false), Is.False);
        }

        [Test]
        public void ADefaultSlotReadsAsEmptyStrings()
        {
            var slot = default(StreakSlot);

            Assert.That(slot.RewardId, Is.EqualTo(string.Empty));
            Assert.That(slot.DisplayNameKey, Is.EqualTo(string.Empty));
            Assert.That(slot.KillsRequired, Is.EqualTo(0));
        }

        [Test]
        public void ASlotRejectsAnEmptyIdAndFewerThanOneKill()
        {
            Assert.Throws<ArgumentException>(() => new StreakSlot(null, "streak.uav", 3, false));
            Assert.Throws<ArgumentException>(() => new StreakSlot(" ", "streak.uav", 3, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakSlot("uav", "streak.uav", 0, false));
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakSlot("uav", "streak.uav", -1, false));
        }

        [Test]
        public void TheStatusCountsTheReadySlotsAndKeepsTheirOrder()
        {
            var slots = new[]
            {
                new StreakSlot("uav", "streak.uav", 3, true),
                new StreakSlot("airstrike", "streak.airstrike", 5, true),
                new StreakSlot("gunship", "streak.gunship", 7, false),
            };

            var status = new StreakStatus(5, slots);

            Assert.That(status.Kills, Is.EqualTo(5));
            Assert.That(status.ReadyCount, Is.EqualTo(2));
            Assert.That(status.Slots, Is.EqualTo(slots));
        }

        [Test]
        public void TheStatusCopiesTheSlotsItIsGiven()
        {
            var slots = new List<StreakSlot> { new StreakSlot("uav", "streak.uav", 3, false) };
            var status = new StreakStatus(1, slots);

            slots[0] = new StreakSlot("uav", "streak.uav", 3, true);
            slots.Add(new StreakSlot("airstrike", "streak.airstrike", 5, true));

            Assert.That(status.Slots.Count, Is.EqualTo(1));
            Assert.That(status.Slots[0].Ready, Is.False);
            Assert.That(status.ReadyCount, Is.EqualTo(0));
        }

        [Test]
        public void NoSlotsAndTheDefaultStatusAreEmpty()
        {
            Assert.That(new StreakStatus(0, null).Slots, Is.Empty);
            Assert.That(default(StreakStatus).Slots, Is.Empty);
            Assert.That(default(StreakStatus).ReadyCount, Is.EqualTo(0));
            Assert.That(new StreakStatus(0, null), Is.EqualTo(default(StreakStatus)));
        }

        [Test]
        public void StatusesWithTheSameSlotsAreEqual()
        {
            var a = new StreakStatus(3, new[] { new StreakSlot("uav", "streak.uav", 3, true) });
            var b = new StreakStatus(3, new List<StreakSlot> { new StreakSlot("uav", "streak.uav", 3, true) });
            var c = new StreakStatus(3, new[] { new StreakSlot("uav", "streak.uav", 3, false) });

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a == c, Is.False);
            Assert.That(a != new StreakStatus(4, new[] { new StreakSlot("uav", "streak.uav", 3, true) }), Is.True);
        }

        [Test]
        public void TheStatusRejectsNegativeKills()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new StreakStatus(-1, null));
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GrenadePouchTests
    {
        private static readonly int[] _expectedChanges = { 1, 0, 2 };

        [Test]
        public void StartsFull()
        {
            var pouch = new GrenadePouch(2);

            Assert.That(pouch.Capacity, Is.EqualTo(2));
            Assert.That(pouch.Remaining, Is.EqualTo(2));
        }

        [Test]
        public void TakesUntilEmptyAndThenRefuses()
        {
            var pouch = new GrenadePouch(2);

            Assert.That(pouch.TryTake(), Is.True);
            Assert.That(pouch.TryTake(), Is.True);
            Assert.That(pouch.TryTake(), Is.False, "a third grenade cannot be thrown");
            Assert.That(pouch.Remaining, Is.Zero);
        }

        [Test]
        public void RefillReturnsToCapacity()
        {
            var pouch = new GrenadePouch(3);
            pouch.TryTake();
            pouch.TryTake();

            pouch.Refill();

            Assert.That(pouch.Remaining, Is.EqualTo(3));
            Assert.That(pouch.TryTake(), Is.True);
        }

        [Test]
        public void ReportsEveryChange()
        {
            var pouch = new GrenadePouch(2);
            var changes = new List<int>();
            pouch.Changed += changes.Add;

            pouch.TryTake();
            pouch.TryTake();
            pouch.TryTake();
            pouch.Refill();
            pouch.Refill();

            Assert.That(changes, Is.EqualTo(_expectedChanges));
        }

        [Test]
        public void EmptyPouchNeverThrows()
        {
            var pouch = new GrenadePouch(0);

            Assert.That(pouch.TryTake(), Is.False);
            pouch.Refill();
            Assert.That(pouch.Remaining, Is.Zero);
        }

        [Test]
        public void ForHoldsTheRuleCountWhenGrenadesAreOn()
        {
            var actions = ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2);

            Assert.That(GrenadePouch.For(actions).Capacity, Is.EqualTo(2));
        }

        [Test]
        public void ForHoldsNothingWhenGrenadesAreOff()
        {
            var actions = ActionRules.Default.WithGrenade(Grenade.Off).WithGrenadeCount(4);

            Assert.That(GrenadePouch.For(actions).Capacity, Is.Zero);
        }

        [Test]
        public void RejectsANegativeCapacity()
        {
            var e = Assert.Throws<ArgumentOutOfRangeException>(() => new GrenadePouch(-1));
            Assert.That(e.ParamName, Is.EqualTo("capacity"));
        }

        [Test]
        public void ForRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => GrenadePouch.For(null));
        }
    }
}

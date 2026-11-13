using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class LoadoutTests
    {
        private sealed class Item
        {
            public Item(string name)
            {
                Name = name;
            }

            public string Name { get; }
        }

        private static readonly Item _rifle = new Item("rifle");
        private static readonly Item _pistol = new Item("pistol");
        private static readonly Item _knife = new Item("knife");

        private static readonly string[] _expectedHolsterCalls = { "rifle@0", "switched 1", "pistol@1", "switched 2" };
        private static readonly int[] _expectedSwitchedIndices = { 2, 0, 2 };

        private static Loadout<Item> Create(float switchSeconds = 0.5f, Action<Item> onHolster = null)
        {
            return new Loadout<Item>(new[] { _rifle, _pistol, _knife }, switchSeconds, onHolster);
        }

        [Test]
        public void StartsHoldingTheFirstItemWithoutSwitching()
        {
            var loadout = Create();

            Assert.That(loadout.Count, Is.EqualTo(3));
            Assert.That(loadout.Items, Is.EqualTo(new[] { _rifle, _pistol, _knife }));
            Assert.That(loadout.CurrentIndex, Is.Zero);
            Assert.That(loadout.Current, Is.SameAs(_rifle));
            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(0.5f));
        }

        [Test]
        public void KeepsItsOwnCopyOfTheItems()
        {
            var items = new List<Item> { _rifle, _pistol };
            var loadout = new Loadout<Item>(items, 0.5f, null);

            items[0] = _knife;
            items.Add(_knife);

            Assert.That(loadout.Count, Is.EqualTo(2));
            Assert.That(loadout.Current, Is.SameAs(_rifle));
        }

        [Test]
        public void SelectChangesTheCurrentItemAndStartsSwitching()
        {
            var loadout = Create();

            Assert.That(loadout.TrySelect(2), Is.True);

            Assert.That(loadout.CurrentIndex, Is.EqualTo(2));
            Assert.That(loadout.Current, Is.SameAs(_knife));
            Assert.That(loadout.IsSwitching, Is.True);
        }

        [TestCase(-1)]
        [TestCase(3)]
        [TestCase(int.MaxValue)]
        [TestCase(int.MinValue)]
        public void SelectOutOfRangeIsRefused(int index)
        {
            var holstered = 0;
            var switched = 0;
            var loadout = Create(onHolster: _ => holstered++);
            loadout.Switched += _ => switched++;

            Assert.That(loadout.TrySelect(index), Is.False);

            Assert.That(loadout.CurrentIndex, Is.Zero);
            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(holstered, Is.Zero);
            Assert.That(switched, Is.Zero);
        }

        [Test]
        public void SelectingTheCurrentIndexIsRefused()
        {
            var holstered = 0;
            var switched = 0;
            var loadout = Create(onHolster: _ => holstered++);
            loadout.Switched += _ => switched++;

            Assert.That(loadout.TrySelect(0), Is.False);

            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(holstered, Is.Zero);
            Assert.That(switched, Is.Zero);
        }

        [Test]
        public void SelectingTheCurrentIndexWhileSwitchingKeepsTheRemainingTime()
        {
            var loadout = Create(switchSeconds: 1f);
            loadout.TrySelect(1);
            loadout.Tick(0.75f);

            Assert.That(loadout.TrySelect(1), Is.False);

            loadout.Tick(0.25f);
            Assert.That(loadout.IsSwitching, Is.False);
        }

        [Test]
        public void SwitchingEndsAfterSwitchSeconds()
        {
            var loadout = Create(switchSeconds: 0.5f);
            loadout.TrySelect(1);

            loadout.Tick(0.25f);
            Assert.That(loadout.IsSwitching, Is.True);

            loadout.Tick(0.25f);
            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(loadout.Current, Is.SameAs(_pistol));
        }

        [Test]
        public void SelectingWhileSwitchingRestartsTheTime()
        {
            var loadout = Create(switchSeconds: 1f);
            loadout.TrySelect(1);
            loadout.Tick(0.75f);

            Assert.That(loadout.TrySelect(2), Is.True);

            loadout.Tick(0.75f);
            Assert.That(loadout.IsSwitching, Is.True);
            Assert.That(loadout.Current, Is.SameAs(_knife));

            loadout.Tick(0.25f);
            Assert.That(loadout.IsSwitching, Is.False);
        }

        [Test]
        public void ZeroSwitchSecondsSwitchesImmediately()
        {
            var loadout = Create(switchSeconds: 0f);

            Assert.That(loadout.TrySelect(1), Is.True);

            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(loadout.Current, Is.SameAs(_pistol));
        }

        [Test]
        public void TickWithoutSwitchingDoesNothing()
        {
            var loadout = Create();

            loadout.Tick(10f);
            loadout.Tick(0f);

            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(loadout.CurrentIndex, Is.Zero);
        }

        [Test]
        public void SelectNextWrapsToTheFirst()
        {
            var loadout = Create();

            Assert.That(loadout.SelectNext(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.EqualTo(1));
            Assert.That(loadout.SelectNext(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.EqualTo(2));
            Assert.That(loadout.SelectNext(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.Zero);
        }

        [Test]
        public void SelectPreviousWrapsToTheLast()
        {
            var loadout = Create();

            Assert.That(loadout.SelectPrevious(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.EqualTo(2));
            Assert.That(loadout.SelectPrevious(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.EqualTo(1));
            Assert.That(loadout.SelectPrevious(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.Zero);
        }

        [Test]
        public void NextAndPreviousWithTwoItemsToggle()
        {
            var loadout = new Loadout<Item>(new[] { _rifle, _pistol }, 0.5f, null);

            Assert.That(loadout.SelectNext(), Is.True);
            Assert.That(loadout.Current, Is.SameAs(_pistol));
            Assert.That(loadout.SelectPrevious(), Is.True);
            Assert.That(loadout.Current, Is.SameAs(_rifle));
            Assert.That(loadout.SelectPrevious(), Is.True);
            Assert.That(loadout.Current, Is.SameAs(_pistol));
        }

        [Test]
        public void NextAndPreviousWithASingleItemAreRefused()
        {
            var holstered = 0;
            var switched = 0;
            var loadout = new Loadout<Item>(new[] { _rifle }, 0.5f, _ => holstered++);
            loadout.Switched += _ => switched++;

            Assert.That(loadout.SelectNext(), Is.False);
            Assert.That(loadout.SelectPrevious(), Is.False);
            Assert.That(loadout.TrySelect(0), Is.False);

            Assert.That(loadout.Current, Is.SameAs(_rifle));
            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(holstered, Is.Zero);
            Assert.That(switched, Is.Zero);
        }

        [Test]
        public void OnHolsterGetsThePreviousItemBeforeTheIndexChanges()
        {
            Loadout<Item> loadout = null;
            var calls = new List<string>();
            loadout = Create(onHolster: item => calls.Add(item.Name + "@" + loadout.CurrentIndex));
            loadout.Switched += index => calls.Add("switched " + index);

            loadout.TrySelect(1);
            loadout.TrySelect(2);

            Assert.That(calls, Is.EqualTo(_expectedHolsterCalls));
        }

        [Test]
        public void SwitchedReportsTheNewIndex()
        {
            var loadout = Create();
            var indices = new List<int>();
            loadout.Switched += indices.Add;

            loadout.TrySelect(2);
            loadout.SelectNext();
            loadout.SelectPrevious();

            Assert.That(indices, Is.EqualTo(_expectedSwitchedIndices));
        }

        [Test]
        public void NullOnHolsterIsAllowed()
        {
            var loadout = Create(onHolster: null);

            Assert.That(loadout.TrySelect(1), Is.True);
        }

        [Test]
        public void RejectsNullItems()
        {
            Assert.Throws<ArgumentNullException>(() => new Loadout<Item>(null, 0.5f, null));
        }

        [Test]
        public void RejectsEmptyItems()
        {
            Assert.Throws<ArgumentException>(() => new Loadout<Item>(Array.Empty<Item>(), 0.5f, null));
        }

        [Test]
        public void RejectsNullElement()
        {
            var ex = Assert.Throws<ArgumentException>(() => new Loadout<Item>(new[] { _rifle, null }, 0.5f, null));

            Assert.That(ex.ParamName, Is.EqualTo("items"));
            Assert.That(ex.Message, Does.Contain("index 1"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void RejectsInvalidSwitchSeconds(float switchSeconds)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new Loadout<Item>(new[] { _rifle }, switchSeconds, null));
        }

        [Test]
        public void CanStartHoldingAGivenIndexWithoutSwitching()
        {
            var holstered = 0;
            var switched = 0;
            var loadout = new Loadout<Item>(new[] { _rifle, _pistol, _knife }, 0.5f, _ => holstered++, 2);
            loadout.Switched += _ => switched++;

            Assert.That(loadout.CurrentIndex, Is.EqualTo(2));
            Assert.That(loadout.Current, Is.SameAs(_knife));
            Assert.That(loadout.IsSwitching, Is.False);
            Assert.That(holstered, Is.Zero);

            Assert.That(loadout.SelectNext(), Is.True);
            Assert.That(loadout.CurrentIndex, Is.Zero);
            Assert.That(holstered, Is.EqualTo(1));
            Assert.That(switched, Is.EqualTo(1));
        }

        [TestCase(-1)]
        [TestCase(3)]
        public void RejectsAnOutOfRangeStartingIndex(int currentIndex)
        {
            var ex = Assert.Throws<ArgumentOutOfRangeException>(
                () => new Loadout<Item>(new[] { _rifle, _pistol, _knife }, 0.5f, null, currentIndex));

            Assert.That(ex.ParamName, Is.EqualTo("currentIndex"));
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void RejectsInvalidDelta(float deltaSeconds)
        {
            var loadout = Create();

            Assert.Throws<ArgumentOutOfRangeException>(() => loadout.Tick(deltaSeconds));
        }
    }
}

using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class GrenadeInventoryTests
    {
        private static readonly GrenadeSpec _smoke = new GrenadeSpec(
            "ecaf6e57-cb77-4707-9321-6ea5b659408f", "grenade.smoke", new ImpactSpec(damage: 0, areaEffect: new AreaEffectSpec(5f, 15f, 0.5f, 0)));

        private static readonly GrenadeCatalog _both =
            new GrenadeCatalog(new[] { GrenadeSpec.Default, _smoke }, new[] { GrenadeSpec.DefaultId, _smoke.Id });

        private static readonly ActionRules _two = ActionRules.Default.WithGrenade(Grenade.On).WithGrenadeCount(2);

        [Test]
        public void EachCarriedTypeGetsTheRulesCount()
        {
            var inventory = new GrenadeInventory(_both, _two);

            Assert.That(inventory.Count, Is.EqualTo(2));
            Assert.That(inventory.Selected, Is.SameAs(GrenadeSpec.Default));
            Assert.That(inventory.CapacityAt(1), Is.EqualTo(2));
            Assert.That(inventory.TotalRemaining, Is.EqualTo(4));
        }

        [Test]
        public void TakingOneTypeLeavesTheOthers()
        {
            var inventory = new GrenadeInventory(_both, _two);

            Assert.That(inventory.TryTake(inventory.IndexOf(_smoke.Id)), Is.True);

            Assert.That(inventory.RemainingAt(1), Is.EqualTo(1));
            Assert.That(inventory.RemainingAt(0), Is.EqualTo(2));
            inventory.Refill();
            Assert.That(inventory.TotalRemaining, Is.EqualTo(4));
        }

        [Test]
        public void SelectNextCyclesThroughTheTypesAndReportsTheChange()
        {
            var inventory = new GrenadeInventory(_both, _two);
            var changes = 0;
            inventory.Changed += () => changes++;

            Assert.That(inventory.SelectNext(), Is.True);
            Assert.That(inventory.Selected, Is.SameAs(_smoke));
            Assert.That(inventory.SelectNext(), Is.True);
            Assert.That(inventory.SelectedIndex, Is.Zero);
            inventory.TryTake(0);

            Assert.That(changes, Is.EqualTo(3));
            Assert.That(inventory.SelectedRemaining, Is.EqualTo(1));
        }

        [Test]
        public void OneTypeCannotBeSwitched()
        {
            var inventory = new GrenadeInventory(GrenadeCatalog.Default, _two);

            Assert.That(inventory.SelectNext(), Is.False);
            Assert.That(inventory.Select(_smoke.Id), Is.False);
            Assert.That(inventory.Select(GrenadeSpec.DefaultId), Is.True);
        }

        [Test]
        public void OffRulesCarryTypesWithNothingInThem()
        {
            var inventory = new GrenadeInventory(_both, ActionRules.Default.WithGrenadeCount(3));

            Assert.That(inventory.Count, Is.EqualTo(2));
            Assert.That(inventory.TotalRemaining, Is.Zero);
            Assert.That(inventory.TryTake(0), Is.False);
        }

        [Test]
        public void TheEmptyInventoryHasNothingSelected()
        {
            var empty = GrenadeInventory.Empty;

            Assert.That(empty.Selected, Is.Null);
            Assert.That(empty.SelectedIndex, Is.EqualTo(-1));
            Assert.That(empty.SelectedRemaining, Is.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => empty.TryTake(0));
        }
    }
}

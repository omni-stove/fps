using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class LoadoutDefinitionTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const string _smgId = "00000000-0000-4000-8000-000000000003";

        private static readonly WeaponCategory[] _primary =
        {
            WeaponCategory.AssaultRifle,
            WeaponCategory.SubmachineGun,
        };

        private static readonly WeaponCategory[] _secondary = { WeaponCategory.Handgun };

        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void ANewLoadoutHasNoSlotsAndTheDefaultSwitchTime()
        {
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            _created.Add(loadout);

            Assert.That(loadout.Slots, Is.Empty);
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(0.4f));
            Assert.That(LoadoutDefinition.DefaultSwitchSeconds, Is.EqualTo(0.4f));
        }

        [Test]
        public void ToRuleKeepsTheSlotOrderAndCategories()
        {
            var loadout = NewLoadout(
                "Loadout",
                new LoadoutSlotDefinition(_primary, Rifle()),
                new LoadoutSlotDefinition(_secondary, Handgun()));

            var rule = loadout.ToRule();

            Assert.That(rule.SlotCount, Is.EqualTo(2));
            Assert.That(rule.Slots[0], Is.EqualTo(_primary));
            Assert.That(rule.Slots[1], Is.EqualTo(_secondary));
            Assert.That(loadout.ToRule(), Is.Not.SameAs(rule));
        }

        [Test]
        public void ToSpecsReturnsTheWeaponsInSlotOrder()
        {
            var loadout = NewLoadout(
                "Loadout",
                new LoadoutSlotDefinition(_primary, Rifle()),
                new LoadoutSlotDefinition(_secondary, Handgun()));

            var specs = loadout.ToSpecs();

            Assert.That(specs, Has.Count.EqualTo(2));
            Assert.That(specs[0].Id, Is.EqualTo(_rifleId));
            Assert.That(specs[1].Id, Is.EqualTo(_handgunId));
            Assert.That(specs[1].Category, Is.EqualTo(WeaponCategory.Handgun));
            Assert.That(loadout.ToSpecs(), Is.Not.SameAs(specs));
        }

        [Test]
        public void SetSlotsCopiesTheListAndTheSlotCopiesItsCategories()
        {
            var categories = new List<WeaponCategory>(_secondary);
            var slot = new LoadoutSlotDefinition(categories, Handgun());
            var source = new List<LoadoutSlotDefinition> { slot };
            var loadout = NewLoadout("Loadout");
            loadout.SetSlots(source);

            source.Add(new LoadoutSlotDefinition(_primary, Rifle()));
            categories.Add(WeaponCategory.Shotgun);

            Assert.That(loadout.Slots, Has.Count.EqualTo(1));
            Assert.That(loadout.Slots[0], Is.SameAs(slot));
            Assert.That(slot.AllowedCategories, Is.EqualTo(_secondary));

            loadout.SetSlots(null);
            Assert.That(loadout.Slots, Is.Empty);
        }

        [Test]
        public void ANullCategoryListReadsAsEmpty()
        {
            var slot = new LoadoutSlotDefinition(null, null);

            Assert.That(slot.AllowedCategories, Is.Empty);
            Assert.That(slot.Weapon, Is.Null);
        }

        [Test]
        public void ToRuleRejectsALoadoutWithoutSlots()
        {
            var loadout = NewLoadout("EmptyLoadout");

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToRule());
            Assert.That(e.Message, Does.Contain("EmptyLoadout"));
            Assert.Throws<WeaponDefinitionException>(() => loadout.ToSpecs());
        }

        [Test]
        public void ToRuleRejectsANullSlot()
        {
            var loadout = NewLoadout("HoleyLoadout", new LoadoutSlotDefinition(_primary, Rifle()), null);

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToRule());
            Assert.That(e.Message, Does.Contain("HoleyLoadout").And.Contain("#1"));
        }

        [Test]
        public void ToRuleRejectsASlotWithoutCategories()
        {
            var loadout = NewLoadout(
                "NoCategoryLoadout",
                new LoadoutSlotDefinition(_primary, Rifle()),
                new LoadoutSlotDefinition(Array.Empty<WeaponCategory>(), Handgun()));

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToRule());
            Assert.That(e.Message, Does.Contain("NoCategoryLoadout").And.Contain("slot 1"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ToRuleRejectsAnUndefinedCategory()
        {
            var loadout = NewLoadout(
                "UndefinedCategoryLoadout",
                new LoadoutSlotDefinition(new[] { (WeaponCategory)99 }, Rifle()));

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToRule());
            Assert.That(e.Message, Does.Contain("UndefinedCategoryLoadout"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [TestCase(-0.1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void ToRuleRejectsAnInvalidSwitchTime(float seconds)
        {
            var loadout = NewLoadout("SlowLoadout", new LoadoutSlotDefinition(_primary, Rifle()));
            loadout.SwitchSeconds = seconds;

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToRule());
            Assert.That(e.Message, Does.Contain("SlowLoadout").And.Contain("switch"));
        }

        [Test]
        public void ToRuleAcceptsAnInstantSwitch()
        {
            var loadout = NewLoadout("InstantLoadout", new LoadoutSlotDefinition(_primary, Rifle()));
            loadout.SwitchSeconds = 0f;

            Assert.That(loadout.ToRule().SlotCount, Is.EqualTo(1));
        }

        [Test]
        public void ToSpecsRejectsAMissingOrDestroyedWeapon()
        {
            var loadout = NewLoadout(
                "UnarmedLoadout",
                new LoadoutSlotDefinition(_primary, Rifle()),
                new LoadoutSlotDefinition(_secondary, null));
            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToSpecs());
            Assert.That(e.Message, Does.Contain("UnarmedLoadout").And.Contain("#1"));

            var gone = Rifle();
            var stale = NewLoadout(
                "StaleLoadout",
                new LoadoutSlotDefinition(_primary, gone),
                new LoadoutSlotDefinition(_secondary, Handgun()));
            Object.DestroyImmediate(gone);
            e = Assert.Throws<WeaponDefinitionException>(() => stale.ToSpecs());
            Assert.That(e.Message, Does.Contain("StaleLoadout").And.Contain("#0"));
        }

        [Test]
        public void ToSpecsRejectsAWeaponTheSlotDoesNotAccept()
        {
            var loadout = NewLoadout(
                "SwappedLoadout",
                new LoadoutSlotDefinition(_primary, Handgun()),
                new LoadoutSlotDefinition(_secondary, Rifle()));

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToSpecs());
            Assert.That(e.Message, Does.Contain("SwappedLoadout").And.Contain("slot 0 does not accept Handgun (weapon 'Handgun')"));
        }

        [Test]
        public void ToSpecsRejectsTheSameWeaponTwice()
        {
            var rifle = Rifle();
            var loadout = NewLoadout(
                "DoubleLoadout",
                new LoadoutSlotDefinition(_primary, rifle),
                new LoadoutSlotDefinition(_primary, rifle));

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToSpecs());
            Assert.That(e.Message, Does.Contain("DoubleLoadout").And.Contain(_rifleId));

            var smg = NewDefinition("Smg", _smgId, WeaponCategory.SubmachineGun);
            loadout.SetSlots(new[]
            {
                new LoadoutSlotDefinition(_primary, rifle),
                new LoadoutSlotDefinition(_primary, smg),
            });
            Assert.That(loadout.ToSpecs(), Has.Count.EqualTo(2));
        }

        [Test]
        public void ToSpecsNamesBothTheLoadoutAndTheBrokenWeapon()
        {
            var broken = Rifle();
            broken.name = "BrokenWeapon";
            broken.MagazineSize = 0;
            var loadout = NewLoadout(
                "MainLoadout",
                new LoadoutSlotDefinition(_primary, broken),
                new LoadoutSlotDefinition(_secondary, Handgun()));

            var e = Assert.Throws<WeaponDefinitionException>(() => loadout.ToSpecs());
            Assert.That(e.Message, Does.Contain("MainLoadout").And.Contain("BrokenWeapon").And.Contain("#0"));
            Assert.That(e.InnerException, Is.InstanceOf<WeaponDefinitionException>());
        }

        private WeaponDefinition Rifle()
        {
            return NewDefinition("Rifle", _rifleId, WeaponCategory.AssaultRifle);
        }

        private WeaponDefinition Handgun()
        {
            return NewDefinition("Handgun", _handgunId, WeaponCategory.Handgun);
        }

        private WeaponDefinition NewDefinition(string assetName, string id, WeaponCategory category)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = assetName;
            definition.Id = id;
            definition.NameKey = assetName;
            definition.Category = category;
            _created.Add(definition);
            return definition;
        }

        private LoadoutDefinition NewLoadout(string assetName, params LoadoutSlotDefinition[] slots)
        {
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            loadout.name = assetName;
            loadout.SetSlots(slots);
            _created.Add(loadout);
            return loadout;
        }
    }
}

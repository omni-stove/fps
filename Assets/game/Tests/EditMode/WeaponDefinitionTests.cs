using System;
using System.Collections.Generic;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    public sealed class WeaponDefinitionTests
    {
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

        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const string _uuidPattern = "^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$";

        [Test]
        public void DefaultsMatchTheRifleExceptTheId()
        {
            var definition = NewDefinition("Default", string.Empty);
            definition.AssignIdIfEmpty();
            var spec = definition.ToSpec();
            var rifle = WeaponAssetBuilder.RifleSpec();

            Assert.That(spec.Id, Does.Match(_uuidPattern));
            Assert.That(spec.DisplayName, Is.EqualTo(spec.Id), "the display name is left empty and falls back to the id");
            Assert.That(spec.Category, Is.EqualTo(rifle.Category));
            Assert.That(spec.FireMode, Is.EqualTo(rifle.FireMode));
            Assert.That(spec.MagazineSize, Is.EqualTo(rifle.MagazineSize));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(rifle.ReserveAmmo));
            Assert.That(spec.FireInterval, Is.EqualTo(rifle.FireInterval));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(rifle.ReloadSeconds));
            Assert.That(spec.Damage, Is.EqualTo(rifle.Damage));
            Assert.That(spec.Range, Is.EqualTo(rifle.Range));
            Assert.That(spec.Falloff.StartDistance, Is.EqualTo(rifle.Falloff.StartDistance));
            Assert.That(spec.Falloff.EndDistance, Is.EqualTo(rifle.Falloff.EndDistance));
            Assert.That(spec.Falloff.MinMultiplier, Is.EqualTo(rifle.Falloff.MinMultiplier));
        }

        [Test]
        public void ANewDefinitionHasNoIdUntilOneIsAssigned()
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(definition);

            // OnValidate が CreateInstance で呼ばれる版でも呼ばれない版でも、空か UUID のどちらか
            Assert.That(definition.Id, Is.Empty.Or.Match(_uuidPattern));

            definition.Id = string.Empty;
            Assert.That(definition.AssignIdIfEmpty(), Is.True);
            Assert.That(definition.Id, Does.Match(_uuidPattern));
            Assert.That(definition.ToSpec().Id, Is.EqualTo(definition.Id));
        }

        [TestCase("")]
        [TestCase("   ")]
        public void AssignIdIfEmptyGivesEachDefinitionADifferentUuid(string empty)
        {
            var first = NewDefinition("First", empty);
            var second = NewDefinition("Second", empty);

            Assert.That(first.AssignIdIfEmpty(), Is.True);
            Assert.That(second.AssignIdIfEmpty(), Is.True);

            Assert.That(first.Id, Does.Match(_uuidPattern));
            Assert.That(second.Id, Does.Match(_uuidPattern));
            Assert.That(second.Id, Is.Not.EqualTo(first.Id));
        }

        [TestCase(_rifleId)]
        [TestCase("not-a-uuid")]
        public void AssignIdIfEmptyKeepsAnExistingId(string id)
        {
            var definition = NewDefinition("Kept", id);

            Assert.That(definition.AssignIdIfEmpty(), Is.False);
            Assert.That(definition.Id, Is.EqualTo(id));
        }

        [Test]
        public void ToSpecCopiesEveryField()
        {
            var definition = NewDefinition("Handgun", " " + _handgunId + " ");
            definition.DisplayName = " Handgun ";
            definition.Category = WeaponCategory.Handgun;
            definition.FireMode = FireMode.Semi;
            definition.MagazineSize = 12;
            definition.ReserveAmmo = 48;
            definition.FireInterval = 0.2f;
            definition.ReloadSeconds = 1.5f;
            definition.Damage = 34;
            definition.Range = 60f;
            definition.FalloffStart = 15f;
            definition.FalloffEnd = 40f;
            definition.FalloffMinMultiplier = 0.6f;

            var spec = definition.ToSpec();

            Assert.That(spec.Id, Is.EqualTo(_handgunId));
            Assert.That(spec.DisplayName, Is.EqualTo("Handgun"));
            Assert.That(spec.Category, Is.EqualTo(WeaponCategory.Handgun));
            Assert.That(spec.FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(spec.MagazineSize, Is.EqualTo(12));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(48));
            Assert.That(spec.FireInterval, Is.EqualTo(0.2f));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(1.5f));
            Assert.That(spec.Damage, Is.EqualTo(34));
            Assert.That(spec.Range, Is.EqualTo(60f));
            Assert.That(spec.Falloff.StartDistance, Is.EqualTo(15f));
            Assert.That(spec.Falloff.EndDistance, Is.EqualTo(40f));
            Assert.That(spec.Falloff.MinMultiplier, Is.EqualTo(0.6f));
            Assert.That(definition.ToSpec(), Is.Not.SameAs(spec));
        }

        [Test]
        public void NullStringsReadAsEmpty()
        {
            var definition = NewDefinition("Nulls");
            definition.Id = null;
            definition.DisplayName = null;

            Assert.That(definition.Id, Is.Empty);
            Assert.That(definition.DisplayName, Is.Empty);
        }

        [TestCase("Id", "")]
        [TestCase("Id", "rifle")]
        [TestCase("Id", "00000000-0000-4000-8000-00000000000A")]
        [TestCase("Category", "")]
        [TestCase("FireMode", "")]
        [TestCase("MagazineSize", "")]
        [TestCase("ReserveAmmo", "")]
        [TestCase("FireInterval", "")]
        [TestCase("ReloadSeconds", "")]
        [TestCase("Damage", "")]
        [TestCase("Range", "")]
        [TestCase("FalloffStart", "")]
        [TestCase("FalloffEnd", "")]
        [TestCase("FalloffMinMultiplier", "")]
        public void ToSpecRejectsInvalidValuesWithTheAssetName(string field, string id)
        {
            var definition = NewDefinition("BrokenWeapon");
            switch (field)
            {
                case "Id":
                    definition.Id = id;
                    break;
                case "Category":
                    definition.Category = (WeaponCategory)99;
                    break;
                case "FireMode":
                    definition.FireMode = (FireMode)99;
                    break;
                case "MagazineSize":
                    definition.MagazineSize = 0;
                    break;
                case "ReserveAmmo":
                    definition.ReserveAmmo = -1;
                    break;
                case "FireInterval":
                    definition.FireInterval = float.NaN;
                    break;
                case "ReloadSeconds":
                    definition.ReloadSeconds = -1f;
                    break;
                case "Damage":
                    definition.Damage = -1;
                    break;
                case "Range":
                    definition.Range = 0f;
                    break;
                case "FalloffStart":
                    definition.FalloffStart = -1f;
                    break;
                case "FalloffEnd":
                    definition.FalloffEnd = definition.FalloffStart - 1f;
                    break;
                case "FalloffMinMultiplier":
                    definition.FalloffMinMultiplier = 1.5f;
                    break;
                default:
                    Assert.Fail("unknown field " + field);
                    break;
            }

            var e = Assert.Throws<WeaponDefinitionException>(() => definition.ToSpec());
            Assert.That(e.Message, Does.Contain("BrokenWeapon"));
            Assert.That(e.InnerException, Is.InstanceOf<ArgumentException>());
        }

        [Test]
        public void ToCatalogKeepsTheOrderAndLooksUpById()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var handgun = NewDefinition("Handgun", _handgunId);
            handgun.FireMode = FireMode.Semi;
            var asset = NewCatalog("Catalog", rifle, handgun);

            var catalog = asset.ToCatalog();

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.All[0].Id, Is.EqualTo(_rifleId));
            Assert.That(catalog.All[1].Id, Is.EqualTo(_handgunId));
            Assert.That(catalog.Get(_handgunId).FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(asset.ToCatalog(), Is.Not.SameAs(catalog));
        }

        [Test]
        public void SetWeaponsCopiesTheList()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var source = new List<WeaponDefinition> { rifle };
            var asset = NewCatalog("Catalog");
            asset.SetWeapons(source);
            source.Add(NewDefinition("Handgun", _handgunId));

            Assert.That(asset.Weapons, Has.Count.EqualTo(1));
            Assert.That(asset.Weapons[0], Is.SameAs(rifle));

            asset.SetWeapons(null);
            Assert.That(asset.Weapons, Is.Empty);
        }

        [Test]
        public void ToCatalogRejectsAnEmptyList()
        {
            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("EmptyCatalog").ToCatalog());
            Assert.That(e.Message, Does.Contain("EmptyCatalog"));
        }

        [Test]
        public void ToCatalogRejectsNullAndDestroyedEntries()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("HoleyCatalog", rifle, null).ToCatalog());
            Assert.That(e.Message, Does.Contain("HoleyCatalog").And.Contain("#1"));

            var destroyed = NewDefinition("Gone", _handgunId);
            var catalog = NewCatalog("StaleCatalog", destroyed, rifle);
            Object.DestroyImmediate(destroyed);
            e = Assert.Throws<WeaponDefinitionException>(() => catalog.ToCatalog());
            Assert.That(e.Message, Does.Contain("StaleCatalog").And.Contain("#0"));
        }

        [Test]
        public void ToCatalogRejectsDuplicateIdsWithBothAssetNames()
        {
            var first = NewDefinition("RifleA", _rifleId);
            var second = NewDefinition("RifleB", " " + _rifleId + " ");

            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("DupCatalog", first, second).ToCatalog());
            Assert.That(e.Message, Does.Contain("DupCatalog").And.Contain("RifleA").And.Contain("RifleB").And.Contain("'" + _rifleId + "'"));
        }

        [Test]
        public void ToCatalogNamesBothTheCatalogAndTheBrokenWeapon()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var broken = NewDefinition("BrokenWeapon", _handgunId);
            broken.MagazineSize = 0;

            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("MainCatalog", rifle, broken).ToCatalog());
            Assert.That(e.Message, Does.Contain("MainCatalog").And.Contain("BrokenWeapon").And.Contain("#1"));
            Assert.That(e.InnerException, Is.InstanceOf<WeaponDefinitionException>());
        }

        private WeaponDefinition NewDefinition(string assetName, string id = _rifleId)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = assetName;
            definition.Id = id;
            _created.Add(definition);
            return definition;
        }

        private WeaponCatalogAsset NewCatalog(string assetName, params WeaponDefinition[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            catalog.name = assetName;
            catalog.SetWeapons(weapons);
            _created.Add(catalog);
            return catalog;
        }
    }
}

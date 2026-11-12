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

        [Test]
        public void DefaultsMatchTheRifle()
        {
            var spec = NewDefinition("Default").ToSpec();
            var rifle = WeaponAssetBuilder.RifleSpec();

            Assert.That(spec.Id, Is.EqualTo(rifle.Id));
            Assert.That(spec.DisplayName, Is.EqualTo(rifle.Id), "the display name is left empty and falls back to the id");
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
        public void ToSpecCopiesEveryField()
        {
            var definition = NewDefinition("Handgun", " handgun ");
            definition.DisplayName = " Handgun ";
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

            Assert.That(spec.Id, Is.EqualTo("handgun"));
            Assert.That(spec.DisplayName, Is.EqualTo("Handgun"));
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
        [TestCase("Id", "Rifle")]
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
            var rifle = NewDefinition("Rifle", "rifle");
            var handgun = NewDefinition("Handgun", "handgun");
            handgun.FireMode = FireMode.Semi;
            var asset = NewCatalog("Catalog", rifle, handgun);

            var catalog = asset.ToCatalog();

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.All[0].Id, Is.EqualTo("rifle"));
            Assert.That(catalog.All[1].Id, Is.EqualTo("handgun"));
            Assert.That(catalog.Get("handgun").FireMode, Is.EqualTo(FireMode.Semi));
            Assert.That(asset.ToCatalog(), Is.Not.SameAs(catalog));
        }

        [Test]
        public void SetWeaponsCopiesTheList()
        {
            var rifle = NewDefinition("Rifle", "rifle");
            var source = new List<WeaponDefinition> { rifle };
            var asset = NewCatalog("Catalog");
            asset.SetWeapons(source);
            source.Add(NewDefinition("Handgun", "handgun"));

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
            var rifle = NewDefinition("Rifle", "rifle");
            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("HoleyCatalog", rifle, null).ToCatalog());
            Assert.That(e.Message, Does.Contain("HoleyCatalog").And.Contain("#1"));

            var destroyed = NewDefinition("Gone", "gone");
            var catalog = NewCatalog("StaleCatalog", destroyed, rifle);
            Object.DestroyImmediate(destroyed);
            e = Assert.Throws<WeaponDefinitionException>(() => catalog.ToCatalog());
            Assert.That(e.Message, Does.Contain("StaleCatalog").And.Contain("#0"));
        }

        [Test]
        public void ToCatalogRejectsDuplicateIdsWithBothAssetNames()
        {
            var first = NewDefinition("RifleA", "rifle");
            var second = NewDefinition("RifleB", " rifle ");

            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("DupCatalog", first, second).ToCatalog());
            Assert.That(e.Message, Does.Contain("DupCatalog").And.Contain("RifleA").And.Contain("RifleB").And.Contain("'rifle'"));
        }

        [Test]
        public void ToCatalogNamesBothTheCatalogAndTheBrokenWeapon()
        {
            var rifle = NewDefinition("Rifle", "rifle");
            var broken = NewDefinition("BrokenWeapon", "broken");
            broken.MagazineSize = 0;

            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("MainCatalog", rifle, broken).ToCatalog());
            Assert.That(e.Message, Does.Contain("MainCatalog").And.Contain("BrokenWeapon").And.Contain("#1"));
            Assert.That(e.InnerException, Is.InstanceOf<WeaponDefinitionException>());
        }

        private WeaponDefinition NewDefinition(string assetName, string id = null)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = assetName;
            if (id != null)
            {
                definition.Id = id;
            }

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

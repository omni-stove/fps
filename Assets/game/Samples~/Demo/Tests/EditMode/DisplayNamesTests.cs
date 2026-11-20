using System;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class DisplayNamesTests
    {
        private const string AmmoId = "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d";

        [Test]
        public void AKeyIsLookedUpAndABlankKeyShowsTheId()
        {
            Assert.That(DisplayNames.Of(" weapon.rifle ", "rifle"), Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(DisplayNames.Of(" ", "rifle"), Is.EqualTo(LocalizedText.Literal("rifle")));
            Assert.That(DisplayNames.Of(null, null), Is.EqualTo(LocalizedText.Literal(string.Empty)));
        }

        [Test]
        public void TheDataKeepsOnlyTheKeyAndTheUiMakesTheName()
        {
            var rifle = TestWeapons.RifleSpec();
            Assert.That(DisplayNames.Of(rifle), Is.EqualTo(new LocalizedText(rifle.NameKey)));
            Assert.That(DisplayNames.Of(new AmmoTypeSpec(AmmoId, "ammo.slug")), Is.EqualTo(new LocalizedText("ammo.slug")));
            Assert.That(DisplayNames.Of(new AmmoTypeSpec(AmmoId, " ")), Is.EqualTo(LocalizedText.Literal(AmmoId)));
            Assert.That(DisplayNames.Of(GrenadeSpec.Default), Is.EqualTo(new LocalizedText("grenade.frag")));
            Assert.That(DisplayNames.Of(new GunGameTier("rifle", " weapon.rifle ")), Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(DisplayNames.Of(new GunGameTier(" rifle ", " ")), Is.EqualTo(LocalizedText.Literal("rifle")));
            Assert.That(Localizer.Resolve(DisplayNames.Of(rifle)), Is.EqualTo("Rifle"));
        }

        [Test]
        public void AssetsAreNamedByTheirKeyOrTheirId()
        {
            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            var mine = ScriptableObject.CreateInstance<MineDeployDefinition>();
            try
            {
                weapon.Id = "handgun-id";
                weapon.NameKey = string.Empty;
                mine.Id = "mine-id";
                mine.NameKey = "deploy.mine";

                Assert.That(DisplayNames.Of(weapon), Is.EqualTo(LocalizedText.Literal("handgun-id")));
                Assert.That(DisplayNames.Of(mine), Is.EqualTo(new LocalizedText("deploy.mine")));

                mine.NameKey = " ";
                Assert.That(DisplayNames.Of(mine), Is.EqualTo(LocalizedText.Literal("mine-id")));
            }
            finally
            {
                Object.DestroyImmediate(weapon);
                Object.DestroyImmediate(mine);
            }
        }

        [Test]
        public void AnAbilityWithoutANameKeyIsNamedByItsId()
        {
            var ability = ScriptableObject.CreateInstance<NullAbilityDefinition>();
            var beacon = ScriptableObject.CreateInstance<BeaconDeployDefinition>();
            try
            {
                ability.Id = "null-ability";
                beacon.Id = "beacon-id";

                Assert.That(DisplayNames.Of((AbilityDefinition)ability), Is.EqualTo(LocalizedText.Literal("null-ability")));
                Assert.That(DisplayNames.Of((AbilityDefinition)beacon), Is.EqualTo(new LocalizedText(BeaconSpec.DefaultNameKey)));
            }
            finally
            {
                Object.DestroyImmediate(ability);
                Object.DestroyImmediate(beacon);
            }
        }

        [Test]
        public void ABuyItemIsNamedByItsKeyOrItsId()
        {
            Assert.That(
                DisplayNames.Of(new BuyItem("rifle-id", BuyItemKind.Weapon, 2700, nameKey: "weapon.rifle")),
                Is.EqualTo(new LocalizedText("weapon.rifle")));
            Assert.That(DisplayNames.Of(new BuyItem("vest", BuyItemKind.Armor, 650, amount: 50)), Is.EqualTo(LocalizedText.Literal("vest")));
            Assert.That(Localizer.Resolve(DisplayNames.Of(new BuyItem("rifle-id", BuyItemKind.Weapon, 2700, nameKey: "weapon.rifle"))), Is.EqualTo("Rifle"));
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Of((BuyItem)null));
        }

        [Test]
        public void EveryItemInTheBundledBuyListHasANameInEveryLanguage()
        {
            foreach (var item in KitTuning.Economy.Catalog.Items)
            {
                var name = DisplayNames.Of(item);
                Assert.That(name.Key, Is.Not.Null.And.Not.Empty, item.Id + " has a name key");
                foreach (var language in GameLanguages.All)
                {
                    Assert.That(Localizer.TableOf(language).Contains(name.Key), Is.True, $"{GameLanguages.Code(language)}: {name.Key}");
                }
            }
        }

        [Test]
        public void AMissionWithoutAKeyHasNoName()
        {
            Assert.That(DisplayNames.Mission(null).IsEmpty, Is.True);
            Assert.That(DisplayNames.Mission(" ").IsEmpty, Is.True);
            Assert.That(DisplayNames.Mission(" campaign.mission.Op.name "), Is.EqualTo(new LocalizedText("campaign.mission.Op.name")));
        }

        [Test]
        public void AnObjectiveDescriptionCarriesTheRequiredCount()
        {
            Assert.That(DisplayNames.Description(new Objective("a", "objective.a")), Is.EqualTo(new LocalizedText("objective.a", 1)));
            Assert.That(DisplayNames.Description(new Objective("c", "key", 4)).Args, Is.EqualTo(new object[] { 4 }));
            Assert.That(DisplayNames.Description(new Objective("b", null)).IsEmpty, Is.True);
        }

        [Test]
        public void NullDataIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Of((WeaponSpec)null));
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Of((WeaponDefinition)null));
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Of((GrenadeSpec)null));
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Of((AmmoTypeSpec)null));
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Of((AbilityDefinition)null));
            Assert.Throws<ArgumentNullException>(() => DisplayNames.Description(null));
        }
    }
}

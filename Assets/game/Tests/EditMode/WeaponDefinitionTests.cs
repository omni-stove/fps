using System;
using System.Collections.Generic;
using System.Linq;
using Shooter.Core;
using Shooter.Editor;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
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
            var rifle = TestWeapons.RifleSpec();

            Assert.That(spec.Id, Does.Match(_uuidPattern));
            Assert.That(spec.NameKey, Is.Empty, "the name key is left empty");
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
            Assert.That(spec.ExplosionRadius, Is.EqualTo(0f));
        }

        [Test]
        public void ActionValuesStartAtTheWeaponSpecDefaults()
        {
            var spec = NewDefinition("Default", _rifleId).ToSpec();

            Assert.That(spec.BlindFireSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.ActiveReloadWindowStart, Is.EqualTo(WeaponSpec.DefaultActiveReloadWindowStart));
            Assert.That(spec.ActiveReloadWindowLength, Is.EqualTo(WeaponSpec.DefaultActiveReloadWindowLength));
            Assert.That(spec.ActiveReloadFailSeconds, Is.EqualTo(WeaponSpec.DefaultActiveReloadFailSeconds));
        }

        [Test]
        public void RecoilRecoveryDelayStartsAtTheWeaponSpecDefault()
        {
            var definition = NewDefinition("Default", _rifleId);

            Assert.That(definition.RecoilRecoveryDelaySeconds, Is.EqualTo(WeaponSpec.DefaultRecoilRecoveryDelaySeconds));
            Assert.That(definition.ToSpec().RecoilRecoveryDelaySeconds, Is.EqualTo(WeaponSpec.DefaultRecoilRecoveryDelaySeconds));
        }

        [Test]
        public void RecoilAndSpreadStartAtZero()
        {
            var definition = NewDefinition("Default", _rifleId);
            var spec = definition.ToSpec();

            Assert.That(definition.RecoilPitchDegrees, Is.EqualTo(0f));
            Assert.That(definition.RecoilYawDegrees, Is.EqualTo(0f));
            Assert.That(definition.RecoilRecoveryPerSecond, Is.EqualTo(0f));
            Assert.That(definition.HipSpreadDegrees, Is.EqualTo(0f));
            Assert.That(definition.AdsSpreadDegrees, Is.EqualTo(0f));
            Assert.That(definition.MoveSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(0f));
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(0f));
            Assert.That(spec.RecoilRecoveryPerSecond, Is.EqualTo(0f));
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(0f));
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(0f));
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
            definition.NameKey = " weapon.handgun ";
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
            definition.ExplosionRadius = 2.5f;
            definition.AdsFieldOfView = 55f;
            definition.BlindFireSpreadDegrees = 12f;
            definition.ActiveReloadWindowStart = 0.45f;
            definition.ActiveReloadWindowLength = 0.2f;
            definition.ActiveReloadFailSeconds = 0.4f;
            definition.RecoilPitchDegrees = 1.5f;
            definition.RecoilYawDegrees = 0.4f;
            definition.RecoilRecoveryPerSecond = 12f;
            definition.HipSpreadDegrees = 3f;
            definition.AdsSpreadDegrees = 0.5f;
            definition.MoveSpreadDegrees = 2f;
            definition.RecoilRecoveryDelaySeconds = 0.3f;
            definition.ShotNoiseRadius = 8f;
            definition.Weight = 1.2f;
            definition.MoveSpeedMultiplier = 1.05f;

            var spec = definition.ToSpec();

            Assert.That(spec.Id, Is.EqualTo(_handgunId));
            Assert.That(spec.NameKey, Is.EqualTo("weapon.handgun"));
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
            Assert.That(spec.ExplosionRadius, Is.EqualTo(2.5f));
            Assert.That(spec.AdsFieldOfView, Is.EqualTo(55f));
            Assert.That(spec.BlindFireSpreadDegrees, Is.EqualTo(12f));
            Assert.That(spec.ActiveReloadWindowStart, Is.EqualTo(0.45f));
            Assert.That(spec.ActiveReloadWindowLength, Is.EqualTo(0.2f));
            Assert.That(spec.ActiveReloadFailSeconds, Is.EqualTo(0.4f));
            Assert.That(spec.RecoilPitchDegrees, Is.EqualTo(1.5f));
            Assert.That(spec.RecoilYawDegrees, Is.EqualTo(0.4f));
            Assert.That(spec.RecoilRecoveryPerSecond, Is.EqualTo(12f));
            Assert.That(spec.HipSpreadDegrees, Is.EqualTo(3f));
            Assert.That(spec.AdsSpreadDegrees, Is.EqualTo(0.5f));
            Assert.That(spec.MoveSpreadDegrees, Is.EqualTo(2f));
            Assert.That(spec.RecoilRecoveryDelaySeconds, Is.EqualTo(0.3f));
            Assert.That(spec.ShotNoiseRadius, Is.EqualTo(8f));
            Assert.That(spec.Weight, Is.EqualTo(1.2f));
            Assert.That(spec.MoveSpeedMultiplier, Is.EqualTo(1.05f));
            Assert.That(definition.ToSpec(), Is.Not.SameAs(spec));
        }

        [Test]
        public void ANewDefinitionUsesNoOptionalPart()
        {
            var spec = NewDefinition("Plain").ToSpec();

            Assert.That(spec.Trigger.Burst, Is.Null);
            Assert.That(spec.Trigger.Charge, Is.Null);
            Assert.That(spec.Trigger.SpinUp, Is.Null);
            Assert.That(spec.Trigger.Cycle, Is.Null);
            Assert.That(spec.Ammo.Kind, Is.EqualTo(AmmoKind.Magazine));
            Assert.That(spec.Ammo.Heat, Is.Null);
            Assert.That(spec.Ammo.ReloadStyle, Is.EqualTo(ReloadStyle.Whole));
            Assert.That(spec.Round.Delivery, Is.EqualTo(RoundDelivery.Hitscan));
            Assert.That(spec.Round.PelletCount, Is.EqualTo(1));
            Assert.That(spec.Round.Projectile, Is.Null);
            Assert.That(spec.Round.Penetration, Is.Null);
            Assert.That(spec.Impact.Explosion, Is.Null);
            Assert.That(spec.Impact.AreaEffect, Is.Null);
            Assert.That(spec.Aim.ZoomLevels, Is.EqualTo(new[] { AdsZoom.DefaultFieldOfView }));
            Assert.That(spec.Aim.BreathHold, Is.Null);
            Assert.That(spec.Aim.Sight, Is.SameAs(SightSpec.Default));
            Assert.That(spec.AmmoTypes, Is.Empty);
        }

        [Test]
        public void HolsterSecondsStartAtZeroAndReachTheSpec()
        {
            var definition = NewDefinition("Holster");
            Assert.That(definition.HolsterSeconds, Is.Zero);
            Assert.That(definition.ToSpec().HolsterSeconds, Is.Zero);

            definition.HolsterSeconds = 0.4f;
            Assert.That(definition.ToSpec().HolsterSeconds, Is.EqualTo(0.4f));
        }

        [Test]
        public void AWeaponIsNotAShieldUntilTheShieldIsTurnedOn()
        {
            var definition = NewDefinition("RiotShield");
            Assert.That(definition.Shield.Enabled, Is.False);
            Assert.That(definition.ToSpec().Shield, Is.Null);

            definition.Shield.Enabled = true;
            var spec = definition.ToSpec();

            Assert.That(spec.IsShield, Is.True);
            Assert.That(spec.Shield.BlockArcDegrees, Is.EqualTo(RiotShieldSpec.DefaultBlockArcDegrees));
            Assert.That(spec.Shield.Width, Is.EqualTo(RiotShieldSpec.DefaultWidth));
            Assert.That(spec.Shield.Height, Is.EqualTo(RiotShieldSpec.DefaultHeight));
            Assert.That(spec.Shield.MoveSpeedMultiplier, Is.EqualTo(RiotShieldSpec.DefaultMoveSpeedMultiplier));
            Assert.That(spec.Shield.Bash.Damage, Is.EqualTo(RiotShieldSpec.DefaultBashDamage));
            Assert.That(spec.Shield.Bash.Reach, Is.EqualTo(RiotShieldSpec.DefaultBashReach));
            Assert.That(spec.Shield.Bash.ArcDegrees, Is.EqualTo(RiotShieldSpec.DefaultBashArcDegrees));
            Assert.That(spec.Shield.Bash.SwingSeconds, Is.EqualTo(RiotShieldSpec.DefaultBashSwingSeconds));
            Assert.That(spec.Shield.Bash.CooldownSeconds, Is.EqualTo(RiotShieldSpec.DefaultBashCooldownSeconds));
            Assert.That(spec.Shield.Bash.ImpactSeconds, Is.EqualTo(RiotShieldSpec.DefaultBashImpactSeconds));
            Assert.That(spec.Shield.Bash.LungeDistance, Is.EqualTo(RiotShieldSpec.DefaultBashLungeDistance));
        }

        [Test]
        public void TheShieldValuesRoundTripThroughTheDefinitionAndKeepThePrefab()
        {
            var definition = NewDefinition("RiotShield");
            var prefab = new GameObject("ShieldLook");
            _created.Add(prefab);
            definition.Shield.Prefab = prefab;
            var source = new WeaponSpec(
                _rifleId,
                "weapon.riotShield",
                WeaponCategory.Handgun,
                shield: new RiotShieldSpec(90f, 0.5f, 1f, 0.7f, new MeleeSpec(30, 1.4f, 70f, 0.5f, 0.3f, 0.2f, 0.3f)));

            WeaponAssets.Configure(definition, source);
            var spec = definition.ToSpec();

            Assert.That(definition.Shield.Prefab, Is.SameAs(prefab), "the look is not in the spec, so configuring keeps it");
            Assert.That(spec.Shield.BlockArcDegrees, Is.EqualTo(90f));
            Assert.That(spec.Shield.Width, Is.EqualTo(0.5f));
            Assert.That(spec.Shield.Height, Is.EqualTo(1f));
            Assert.That(spec.Shield.MoveSpeedMultiplier, Is.EqualTo(0.7f));
            Assert.That(spec.Shield.Bash.Damage, Is.EqualTo(30));
            Assert.That(spec.Shield.Bash.Reach, Is.EqualTo(1.4f));
            Assert.That(spec.Shield.Bash.ArcDegrees, Is.EqualTo(70f));
            Assert.That(spec.Shield.Bash.SwingSeconds, Is.EqualTo(0.5f));
            Assert.That(spec.Shield.Bash.CooldownSeconds, Is.EqualTo(0.3f));
            Assert.That(spec.Shield.Bash.ImpactSeconds, Is.EqualTo(0.2f));
            Assert.That(spec.Shield.Bash.LungeDistance, Is.EqualTo(0.3f));

            WeaponAssets.Configure(definition, TestWeapons.RifleSpec());
            Assert.That(definition.ToSpec().Shield, Is.Null, "configuring a weapon without a shield turns it off");
        }

        [Test]
        public void ABadShieldValueNamesTheWeapon()
        {
            var definition = NewDefinition("BadShield");
            definition.Shield.Enabled = true;
            definition.Shield.MoveSpeedMultiplier = 1.5f;

            var e = Assert.Throws<WeaponDefinitionException>(() => definition.ToSpec());

            Assert.That(e.Message, Does.Contain("BadShield").And.Contain("moveSpeedMultiplier"));
        }

        [Test]
        public void DrawSecondsAreUnsetUntilGivenAndZeroIsKept()
        {
            var definition = NewDefinition("Draw");
            Assert.That(definition.DrawSeconds, Is.Null);
            Assert.That(definition.ToSpec().DrawSeconds, Is.Null, "a weapon without its own draw time uses the loadout's");

            definition.DrawSeconds = 0f;
            Assert.That(definition.ToSpec().DrawSeconds, Is.EqualTo(0f), "zero draws instantly and is not the same as unset");

            definition.DrawSeconds = 0.3f;
            Assert.That(definition.ToSpec().DrawSeconds, Is.EqualTo(0.3f));

            definition.DrawSeconds = null;
            Assert.That(definition.ToSpec().DrawSeconds, Is.Null);
        }

        [Test]
        public void ConfiguringFromASpecWithEveryPartReadsBackTheSame()
        {
            var expected = TestWeapons.EveryPartSpec();
            var definition = NewDefinition("Everything");

            WeaponAssets.Configure(definition, expected);

            Assert.That(WeaponSpecJson.Write(definition.ToSpec()), Is.EqualTo(WeaponSpecJson.Write(expected)));
        }

        [Test]
        public void TheRoundOfAnotherDeliveryIsKeptButNotUsed()
        {
            var definition = NewDefinition("Hitscan");
            definition.Projectile.Speed = 30f;
            definition.Beam.TickSeconds = 0.2f;

            Assert.That(definition.ToSpec().Round.Projectile, Is.Null);
            Assert.That(definition.ToSpec().Round.Beam, Is.Null);

            definition.Delivery = RoundDelivery.Projectile;
            Assert.That(definition.ToSpec().Round.Projectile.Speed, Is.EqualTo(30f));
            Assert.That(definition.ToSpec().Round.Beam, Is.Null);
        }

        [Test]
        public void TheSightIsUsedOnlyWhileEnabled()
        {
            var definition = NewDefinition("Scoped");
            definition.Sight.ReticleId = "mildot";
            definition.Sight.ScopeOverlay = true;
            Assert.That(definition.ToSpec().Aim.Sight, Is.SameAs(SightSpec.Default), "kept but off");

            definition.Sight.Enabled = true;
            var sight = definition.ToSpec().Aim.Sight;
            Assert.That(sight.ReticleId, Is.EqualTo("mildot"));
            Assert.That(sight.ScopeOverlay, Is.True);
            Assert.That(sight.EyeViewInThirdPerson, Is.False);
            Assert.That(sight.AimedInProgress, Is.EqualTo(SightSpec.DefaultAimedInProgress));
            Assert.That(sight.VisionMode, Is.EqualTo(VisionMode.None));

            definition.Sight.Set(SightSpec.Default);
            Assert.That(definition.Sight.Enabled, Is.False);
        }

        [Test]
        public void TheSightsVisionModeGoesIntoTheSpecAndBack()
        {
            var definition = NewDefinition("Thermal");
            Assert.That(definition.Sight.VisionMode, Is.EqualTo(VisionMode.None));

            definition.Sight.Enabled = true;
            definition.Sight.VisionMode = VisionMode.Thermal;
            var sight = definition.ToSpec().Aim.Sight;
            Assert.That(sight.VisionMode, Is.EqualTo(VisionMode.Thermal));
            Assert.That(sight.IsDefault, Is.False, "a vision mode alone makes the sight worth keeping");

            var other = NewDefinition("Night");
            other.Sight.Set(new SightSpec("nv", visionMode: VisionMode.Night));
            Assert.That(other.Sight.Enabled, Is.True);
            Assert.That(other.Sight.VisionMode, Is.EqualTo(VisionMode.Night));
            Assert.That(other.ToSpec().Aim.Sight.VisionMode, Is.EqualTo(VisionMode.Night));
        }

        [Test]
        public void ExtraZoomLevelsFollowTheAdsFieldOfView()
        {
            var definition = NewDefinition("Scoped");
            definition.ExtraZoomLevels = new[] { 20f, 10f };

            Assert.That(definition.ToSpec().Aim.ZoomLevels, Is.EqualTo(new[] { AdsZoom.DefaultFieldOfView, 20f, 10f }));

            definition.AdsFieldOfView = 40f;
            Assert.That(definition.ToSpec().Aim.ZoomLevels, Is.EqualTo(new[] { 40f, 20f, 10f }));
        }

        [Test]
        public void ConfiguringKeepsTheVisualAndSoundReferences()
        {
            var expected = TestWeapons.EveryPartSpec();
            var definition = NewDefinition("Everything");
            WeaponAssets.Configure(definition, expected);
            var visual = new GameObject("BoltVisual");
            _created.Add(visual);
            var sound = AudioClip.Create("Boom", 1, 1, 8000, false);
            _created.Add(sound);
            definition.Projectile.Visual = visual;
            definition.Explosion.Sound = sound;
            var piercing = definition.AmmoTypes.Single(a => a.Id == TestWeapons.EveryPartPiercingAmmoId);
            piercing.Round.Spray.Visual = visual;

            WeaponAssets.Configure(definition, expected);

            Assert.That(definition.Projectile.Visual, Is.SameAs(visual));
            Assert.That(definition.Explosion.Sound, Is.SameAs(sound));
            Assert.That(definition.AmmoTypes.Single(a => a.Id == TestWeapons.EveryPartPiercingAmmoId).Round.Spray.Visual, Is.SameAs(visual));
        }

        [Test]
        public void AnInvalidPartNamesTheAsset()
        {
            var definition = NewDefinition("BadBurst");
            definition.Burst.Enabled = true;
            definition.Burst.RoundsPerBurst = 1;

            var e = Assert.Throws<WeaponDefinitionException>(() => definition.ToSpec());

            Assert.That(e.Message, Does.Contain("BadBurst").And.Contain("roundsPerBurst"));
        }

        [Test]
        public void AnUnsetAdsFieldOfViewUsesTheCommonDefault()
        {
            var definition = NewDefinition("Unset", _rifleId);

            Assert.That(definition.AdsFieldOfView, Is.EqualTo(WeaponDefinition.UnsetAdsFieldOfView));
            Assert.That(definition.ToSpec().AdsFieldOfView, Is.EqualTo(AdsZoom.DefaultFieldOfView));
        }

        [Test]
        public void NullStringsReadAsEmpty()
        {
            var definition = NewDefinition("Nulls");
            definition.Id = null;
            definition.NameKey = null;

            Assert.That(definition.Id, Is.Empty);
            Assert.That(definition.NameKey, Is.Empty);
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
        [TestCase("ExplosionRadius", "")]
        [TestCase("AdsFieldOfView", "")]
        [TestCase("BlindFireSpreadDegrees", "")]
        [TestCase("ActiveReloadWindowStart", "")]
        [TestCase("ActiveReloadWindowLength", "")]
        [TestCase("ActiveReloadWindowEnd", "")]
        [TestCase("ActiveReloadFailSeconds", "")]
        [TestCase("RecoilPitchDegrees", "")]
        [TestCase("RecoilYawDegrees", "")]
        [TestCase("RecoilRecoveryPerSecond", "")]
        [TestCase("HipSpreadDegrees", "")]
        [TestCase("AdsSpreadDegrees", "")]
        [TestCase("MoveSpreadDegrees", "")]
        [TestCase("RecoilRecoveryDelaySeconds", "")]
        [TestCase("ShotNoiseRadius", "")]
        [TestCase("Weight", "")]
        [TestCase("MoveSpeedMultiplier", "")]
        [TestCase("DrawSeconds", "")]
        [TestCase("HolsterSeconds", "")]
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
                case "ExplosionRadius":
                    definition.ExplosionRadius = -1f;
                    break;
                case "AdsFieldOfView":
                    definition.AdsFieldOfView = 180f;
                    break;
                case "BlindFireSpreadDegrees":
                    definition.BlindFireSpreadDegrees = 91f;
                    break;
                case "ActiveReloadWindowStart":
                    definition.ActiveReloadWindowStart = 1f;
                    break;
                case "ActiveReloadWindowLength":
                    definition.ActiveReloadWindowLength = 0f;
                    break;
                case "ActiveReloadWindowEnd":
                    definition.ActiveReloadWindowStart = 0.9f;
                    definition.ActiveReloadWindowLength = 0.2f;
                    break;
                case "ActiveReloadFailSeconds":
                    definition.ActiveReloadFailSeconds = -0.1f;
                    break;
                case "RecoilPitchDegrees":
                    definition.RecoilPitchDegrees = 91f;
                    break;
                case "RecoilYawDegrees":
                    definition.RecoilYawDegrees = -0.1f;
                    break;
                case "RecoilRecoveryPerSecond":
                    definition.RecoilRecoveryPerSecond = float.NaN;
                    break;
                case "HipSpreadDegrees":
                    definition.HipSpreadDegrees = 91f;
                    break;
                case "AdsSpreadDegrees":
                    definition.AdsSpreadDegrees = -0.1f;
                    break;
                case "MoveSpreadDegrees":
                    definition.MoveSpreadDegrees = float.PositiveInfinity;
                    break;
                case "RecoilRecoveryDelaySeconds":
                    definition.RecoilRecoveryDelaySeconds = -0.1f;
                    break;
                case "ShotNoiseRadius":
                    definition.ShotNoiseRadius = float.NaN;
                    break;
                case "Weight":
                    definition.Weight = -1f;
                    break;
                case "MoveSpeedMultiplier":
                    definition.MoveSpeedMultiplier = float.NaN;
                    break;
                case "DrawSeconds":
                    definition.DrawSeconds = -0.1f;
                    break;
                case "HolsterSeconds":
                    definition.HolsterSeconds = -0.1f;
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
        public void TryFindReturnsTheDefinitionWithTheId()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var handgun = NewDefinition("Handgun", " " + _handgunId + " ");
            var asset = NewCatalog("Catalog", rifle, handgun);

            Assert.That(asset.TryFind(_rifleId, out var found), Is.True);
            Assert.That(found, Is.SameAs(rifle));
            Assert.That(asset.TryFind(_handgunId, out found), Is.True, "compares the id without surrounding spaces");
            Assert.That(found, Is.SameAs(handgun));

            Assert.That(asset.TryFind("00000000-0000-4000-8000-000000000003", out found), Is.False);
            Assert.That(found, Is.Null);
            Assert.That(asset.TryFind(null, out found), Is.False);
            Assert.That(found, Is.Null);
        }

        [Test]
        public void TryFindRejectsAnInvalidCatalogEvenForAnIdItHas()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var broken = NewDefinition("BrokenWeapon", _handgunId);
            broken.MagazineSize = 0;

            var e = Assert.Throws<WeaponDefinitionException>(() => NewCatalog("MainCatalog", rifle, broken).TryFind(_rifleId, out _));
            Assert.That(e.Message, Does.Contain("MainCatalog").And.Contain("BrokenWeapon"));
        }

        [Test]
        public void SetWeaponsCopiesTheList()
        {
            var rifle = NewDefinition("Rifle", _rifleId);
            var source = new List<WeaponDefinition> { rifle };
            var asset = NewCatalog("Catalog");
            asset.SetWeapons(source);
            source.Add(NewDefinition("Handgun", _handgunId));

            Assert.That(asset.Weapons.Count(), Is.EqualTo(1));
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

        [Test]
        public void TheWindUpSoundRisesFromTheStartToTheFull()
        {
            var sound = new WindUpSoundSettings(0.5f, 1.5f, 0.2f);

            Assert.That(sound.PitchAt(0f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(sound.PitchAt(0.5f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(sound.PitchAt(2f), Is.EqualTo(1.5f).Within(1e-5f), "clamped at full");
            Assert.That(sound.VolumeAt(0f), Is.EqualTo(0.2f).Within(1e-5f));
            Assert.That(sound.VolumeAt(1f), Is.EqualTo(1f).Within(1e-5f));
        }
    }
}

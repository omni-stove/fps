using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class AmmoModelsTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-000000000001";
        private const string _slugId = "00000000-0000-4000-8000-0000000000a1";
        private const string _standardId = "00000000-0000-4000-8000-0000000000a2";
        private const string _piercingId = "00000000-0000-4000-8000-0000000000a3";

        private static WeaponSpec TwoTypes()
        {
            return new WeaponSpec(
                _rifleId,
                null,
                WeaponCategory.SniperRifle,
                ammo: new AmmoSpec(5, 10, 1f),
                impact: new ImpactSpec(40),
                ammoTypes: new[]
                {
                    new AmmoTypeSpec(_standardId, "ammo.dmr.standard"),
                    new AmmoTypeSpec(
                        _piercingId,
                        "ammo.dmr.armorPiercing",
                        new RoundSpec(RoundDelivery.Hitscan, penetration: new PenetrationSpec(2, 0.75f, 0.5f)),
                        new ImpactSpec(30)),
                });
        }

        private static AmmoSpec Heat()
        {
            return new AmmoSpec(5, 10, 1f, kind: AmmoKind.Heat, heat: new HeatSpec(0.2f, 0.5f, 0.25f, 1f));
        }

        private static AmmoSpec PerRound()
        {
            return new AmmoSpec(5, 10, 1f, reloadStyle: ReloadStyle.PerRound, reloadSecondsPerRound: 0.5f);
        }

        [Test]
        public void PicksTheModelFromTheAmmoKindAndReloadStyle()
        {
            Assert.That(AmmoModels.For(AmmoSpec.Default, GameRules.Default), Is.TypeOf<MagazineAmmo>());
            Assert.That(AmmoModels.For(Heat(), GameRules.Default), Is.TypeOf<HeatAmmo>());
            Assert.That(AmmoModels.For(PerRound(), GameRules.Default), Is.TypeOf<PerRoundReload>());
            Assert.That(AmmoModels.For(PerRound(), 2, 3, GameRules.Default), Is.TypeOf<PerRoundReload>());
        }

        [Test]
        public void TheMagazineFiresReloadsAndShowsNoHeat()
        {
            var ammo = new MagazineAmmo(new AmmoSpec(2, 3, 1f), GameRules.Default);
            var reloaded = 0;
            ammo.Reloaded += () => reloaded++;

            Assert.That(ammo.HeatFraction, Is.Null);
            ammo.Consume();
            ammo.Consume();
            Assert.That(ammo.CanFire, Is.False);
            Assert.That(ammo.TryStartReload(), Is.True);
            Assert.That(ammo.CanFire, Is.False);
            ammo.Tick(0.5f);
            Assert.That(ammo.ReloadProgress, Is.EqualTo(0.5f));
            ammo.Tick(0.5f);
            Assert.That(reloaded, Is.EqualTo(1));
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(1));
            Assert.That(ammo.CanFire, Is.True);
        }

        [Test]
        public void ConsumingWithoutARoundIsAnError()
        {
            var ammo = new MagazineAmmo(new AmmoSpec(1, 0, 1f), 0, 0, GameRules.Default);

            Assert.Throws<InvalidOperationException>(ammo.Consume);
        }

        [Test]
        public void ThePerRoundModelLoadsOneRoundEachIntervalUntilTheMagazineIsFull()
        {
            var ammo = AmmoModels.For(PerRound(), 2, 10, GameRules.Default);
            var reloaded = 0;
            ammo.Reloaded += () => reloaded++;

            Assert.That(ammo.HeatFraction, Is.Null);
            Assert.That(ammo.TryStartReload(), Is.True);
            Assert.That(ammo.IsReloading, Is.True);

            ammo.Tick(0.25f);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(2), "half a round's time loads nothing yet");
            Assert.That(ammo.ReloadProgress, Is.EqualTo(0.5f / 3f).Within(1e-5f));

            ammo.Tick(0.25f);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(3));
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(9));
            Assert.That(reloaded, Is.EqualTo(1), "each loaded round is announced");
            Assert.That(ammo.IsReloading, Is.True);

            ammo.Tick(0.5f);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(4));
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(8));

            ammo.Tick(0.5f);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(7));
            Assert.That(reloaded, Is.EqualTo(3));
            Assert.That(ammo.IsReloading, Is.False, "a full magazine ends the reload");
            Assert.That(ammo.ReloadProgress, Is.Zero);
        }

        [Test]
        public void ALongFrameLoadsEveryRoundItCovers()
        {
            var ammo = AmmoModels.For(PerRound(), 0, 10, GameRules.Default);

            Assert.That(ammo.TryStartReload(), Is.True);
            ammo.Tick(1.25f);

            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(8));
            Assert.That(ammo.IsReloading, Is.True);
        }

        [Test]
        public void FiringDuringAPerRoundReloadStopsItWithTheRoundsLoadedSoFar()
        {
            var ammo = AmmoModels.For(PerRound(), 0, 10, GameRules.Default);

            Assert.That(ammo.TryStartReload(), Is.True);
            Assert.That(ammo.CanFire, Is.False, "nothing is loaded yet");
            ammo.Tick(1f);
            Assert.That(ammo.CanFire, Is.True, "a loaded round can be fired mid-reload");

            ammo.Consume();

            Assert.That(ammo.IsReloading, Is.False);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(1));
            ammo.Tick(2f);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(1), "the stopped reload loads no more");
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(8));
        }

        [Test]
        public void APerRoundReloadStopsWhenTheReserveRunsOut()
        {
            var ammo = AmmoModels.For(PerRound(), 0, 2, GameRules.Default);

            Assert.That(ammo.TryStartReload(), Is.True);
            ammo.Tick(2f);

            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(ammo.ReserveAmmo, Is.Zero);
            Assert.That(ammo.IsReloading, Is.False);
            Assert.That(ammo.TryStartReload(), Is.False);
        }

        [Test]
        public void APerRoundReloadWithAnInfiniteReserveKeepsTheReserve()
        {
            var ammo = AmmoModels.For(PerRound(), 0, 0, GameRules.Default.WithInfiniteReserve(true));

            Assert.That(ammo.TryStartReload(), Is.True);
            ammo.Tick(2.5f);

            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(ammo.ReserveAmmo, Is.Zero);
        }

        [Test]
        public void APerRoundReloadDoesNotStartWhenFullOrAlreadyReloading()
        {
            var ammo = AmmoModels.For(PerRound(), GameRules.Default);
            Assert.That(ammo.TryStartReload(), Is.False, "the magazine is full");

            ammo.Consume();
            Assert.That(ammo.TryStartReload(), Is.True);
            Assert.That(ammo.TryStartReload(), Is.False);
        }

        [Test]
        public void APerRoundReloadHasNoActiveReloadEvenWhenTheRulesTurnItOn()
        {
            var rules = GameRules.Default.WithActions(GameRules.Default.Actions.WithActiveReload(ActiveReload.On));
            var ammo = AmmoModels.For(PerRound(), 0, 10, rules);

            Assert.That(ammo.TryStartReload(), Is.True);
            Assert.That(ammo.HasActiveReload, Is.False);
            Assert.That(ammo.TryActiveReload(out var result), Is.False);
            Assert.That(result, Is.EqualTo(ActiveReloadResult.Fail));
            Assert.That(ammo.HasJudgedActiveReload, Is.False);
        }

        [Test]
        public void CancellingAPerRoundReloadKeepsTheLoadedRounds()
        {
            var ammo = AmmoModels.For(PerRound(), 0, 10, GameRules.Default);

            ammo.TryStartReload();
            ammo.Tick(0.75f);
            ammo.CancelReload();
            ammo.Tick(1f);

            Assert.That(ammo.IsReloading, Is.False);
            Assert.That(ammo.AmmoInMagazine, Is.EqualTo(1));
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(9));
        }

        [Test]
        public void TheWeaponFiresAPerRoundRoundMidReload()
        {
            var weapon = new Weapon(new WeaponSpec(_rifleId, null, WeaponCategory.Shotgun, ammo: PerRound()), 0, 10);

            Assert.That(weapon.TryStartReload(), Is.True);
            Assert.That(weapon.TryFire(), Is.False);
            weapon.Tick(0.5f);

            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
        }

        [Test]
        public void TheWeaponHandsItsAmmoToTheModel()
        {
            var weapon = new Weapon(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, ammo: new AmmoSpec(3, 6, 1f)));

            Assert.That(weapon.Ammo, Is.TypeOf<MagazineAmmo>());
            Assert.That(weapon.TryFire(), Is.True);
            Assert.That(weapon.Ammo.AmmoInMagazine, Is.EqualTo(2));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(2));
        }

        [Test]
        public void AWeaponWithoutAmmoTypesKeepsItsOwnRoundAndImpact()
        {
            var spec = new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle);
            var selector = new AmmoTypeSelector(spec);

            Assert.That(selector.TrySwitch(), Is.False);
            Assert.That(selector.Index, Is.EqualTo(-1));
            Assert.That(selector.Current, Is.Null);
            Assert.That(selector.Round, Is.SameAs(spec.Round));
            Assert.That(selector.Impact, Is.SameAs(spec.Impact));
        }

        [Test]
        public void AWeaponWithOneAmmoTypeLoadsItAndHasNothingToSwitchTo()
        {
            var slug = new AmmoTypeSpec(_slugId, "ammo.slug", impact: new ImpactSpec(90));
            var selector = new AmmoTypeSelector(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, ammoTypes: new[] { slug }));

            Assert.That(selector.Types, Is.EqualTo(new[] { slug }));
            Assert.That(selector.Index, Is.Zero);
            Assert.That(selector.Current, Is.SameAs(slug));
            Assert.That(selector.Impact.Damage, Is.EqualTo(90));
            Assert.That(selector.TrySwitch(), Is.False);
            Assert.That(selector.Index, Is.Zero);
        }

        [Test]
        public void SwitchingCyclesThroughTheTypesInOrderAndBack()
        {
            var spec = TwoTypes();
            var selector = new AmmoTypeSelector(spec);

            Assert.That(selector.Index, Is.Zero, "starts with the first type");
            Assert.That(selector.Current.Id, Is.EqualTo(_standardId));
            Assert.That(selector.Round, Is.SameAs(spec.Round), "a type without a round fires the weapon's round");
            Assert.That(selector.Impact, Is.SameAs(spec.Impact));

            Assert.That(selector.TrySwitch(), Is.True);
            Assert.That(selector.Index, Is.EqualTo(1));
            Assert.That(selector.Current.Id, Is.EqualTo(_piercingId));
            Assert.That(selector.Round.Penetration.MaxPenetrations, Is.EqualTo(2));
            Assert.That(selector.Impact.Damage, Is.EqualTo(30));

            Assert.That(selector.TrySwitch(), Is.True);
            Assert.That(selector.Index, Is.Zero, "wraps around to the first type");
            Assert.That(selector.Round, Is.SameAs(spec.Round));
            Assert.That(selector.Impact.Damage, Is.EqualTo(40));
        }

        [Test]
        public void SelectingATypeKeepsTheMagazine()
        {
            var weapon = new Weapon(TwoTypes());

            Assert.That(weapon.AmmoTypes.TrySelect(1), Is.True);

            Assert.That(weapon.AmmoTypes.Current.Id, Is.EqualTo(_piercingId));
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.AmmoTypes.TrySelect(-1), Is.False);
            Assert.That(weapon.AmmoTypes.TrySelect(2), Is.False);
            Assert.That(weapon.AmmoTypes.Index, Is.EqualTo(1), "a rejected index keeps the choice");

            var plain = new AmmoTypeSelector(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle));
            Assert.That(plain.TrySelect(-1), Is.False);
            Assert.That(plain.TrySelect(0), Is.False);
            Assert.That(plain.Index, Is.EqualTo(-1));
        }

        [Test]
        public void SwitchingTheWeaponsAmmoTypeEmptiesTheSharedMagazineSoTheNextRoundsNeedAReload()
        {
            var weapon = new Weapon(TwoTypes());
            var switched = 0;
            weapon.AmmoTypeSwitched += () => switched++;
            Assert.That(weapon.TryFire(), Is.True);

            Assert.That(weapon.TrySwitchAmmoType(), Is.True);

            Assert.That(switched, Is.EqualTo(1));
            Assert.That(weapon.AmmoTypes.Impact.Damage, Is.EqualTo(30));
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10 + 4), "the loaded rounds go back to the reserve");
            Assert.That(weapon.TryFire(), Is.False, "the switched rounds are not loaded yet");

            Assert.That(weapon.TryStartReload(), Is.True);
            weapon.Tick(1f);

            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(9));
            Assert.That(weapon.TryFire(), Is.True);
        }

        [Test]
        public void SwitchingTheAmmoTypeCancelsAReload()
        {
            var weapon = new Weapon(TwoTypes());
            weapon.TryFire();
            weapon.TryStartReload();
            var reloaded = 0;
            weapon.Reloaded += () => reloaded++;

            Assert.That(weapon.TrySwitchAmmoType(), Is.True);
            weapon.Tick(1f);

            Assert.That(weapon.IsReloading, Is.False);
            Assert.That(reloaded, Is.Zero);
            Assert.That(weapon.AmmoInMagazine, Is.Zero);
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(14));
        }

        [Test]
        public void AWeaponWithNothingToSwitchToKeepsItsMagazine()
        {
            var weapon = new Weapon(new WeaponSpec(_rifleId, null, WeaponCategory.AssaultRifle, ammo: new AmmoSpec(5, 10, 1f)));
            var switched = 0;
            weapon.AmmoTypeSwitched += () => switched++;

            Assert.That(weapon.TrySwitchAmmoType(), Is.False);
            Assert.That(weapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(10));
            Assert.That(switched, Is.Zero);
        }

        [Test]
        public void UnloadingWithAnInfiniteReserveOnlyEmptiesTheMagazine()
        {
            var infinite = GameRules.Default.WithInfiniteReserve(true);
            var ammo = new MagazineAmmo(new AmmoSpec(5, 10, 1f), infinite);

            ammo.Unload();

            Assert.That(ammo.AmmoInMagazine, Is.Zero);
            Assert.That(ammo.ReserveAmmo, Is.EqualTo(10));
            Assert.That(ammo.TryStartReload(), Is.True);
        }

        [Test]
        public void EveryAmmoModelUnloadsIntoTheReserve()
        {
            foreach (var model in new[] { AmmoModels.For(Heat(), GameRules.Default), AmmoModels.For(PerRound(), GameRules.Default) })
            {
                model.Unload();

                Assert.That(model.AmmoInMagazine, Is.Zero, model.GetType().Name);
                Assert.That(model.ReserveAmmo, Is.EqualTo(15), model.GetType().Name);
            }
        }

        [Test]
        public void RejectsInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => new MagazineAmmo(null, GameRules.Default));
            Assert.Throws<ArgumentNullException>(() => new MagazineAmmo(AmmoSpec.Default, null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MagazineAmmo(AmmoSpec.Default, 31, 0, GameRules.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MagazineAmmo(AmmoSpec.Default, 0, -1, GameRules.Default));
            Assert.Throws<ArgumentOutOfRangeException>(() => new MagazineAmmo(AmmoSpec.Default, GameRules.Default).Tick(-1f));
            Assert.Throws<ArgumentNullException>(() => new HeatAmmo(null, new MagazineAmmo(AmmoSpec.Default, GameRules.Default)));
            Assert.Throws<ArgumentNullException>(() => new PerRoundReload(AmmoSpec.Default, null));
            Assert.Throws<ArgumentNullException>(() => new AmmoTypeSelector(null));
        }
    }
}

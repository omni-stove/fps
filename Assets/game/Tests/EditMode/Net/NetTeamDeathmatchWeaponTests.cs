using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦の武器がロードアウトのアセット (<see cref="LoadoutDefinition"/>) から配られ、射撃が撃った人の今の武器で判定されるか。
    /// 試合の開始時に <see cref="NetTeamDeathmatch"/> が使うのと同じ経路 (<see cref="NetTeamDeathmatch.TryCreateLoadout"/>) で読み、
    /// 射撃の武器の検査は <see cref="NetTeamDeathmatch.ServerFire"/> と同じ <see cref="NetTeamDeathmatch.CheckWeaponShot"/> を、
    /// Spawn していない <see cref="NetPlayerWeapon"/> (サーバー側の状態だけを持つ) で確かめる。
    /// </summary>
    public sealed class NetTeamDeathmatchWeaponTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-00000000000a";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";

        // ロードアウトに無い Id
        private const string _shotgunId = "00000000-0000-4000-8000-000000000003";

        private const int _rifleDamage = 25;
        private const int _handgunDamage = 34;
        private const float _rifleInterval = 0.1f;
        private const float _handgunInterval = 0.3f;
        private const float _rifleRange = 120f;
        private const float _handgunRange = 40f;
        private const float _switchSeconds = 0.4f;
        private const int _shooterId = 1;

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

        private WeaponDefinition Rifle()
        {
            return Definition("Rifle", _rifleId, WeaponCategory.AssaultRifle, FireMode.Auto, _rifleDamage, _rifleInterval, _rifleRange);
        }

        private WeaponDefinition Handgun()
        {
            return Definition("Handgun", _handgunId, WeaponCategory.Handgun, FireMode.Semi, _handgunDamage, _handgunInterval, _handgunRange);
        }

        private WeaponDefinition Definition(
            string assetName, string id, WeaponCategory category, FireMode fireMode, int damage, float fireInterval, float range)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(definition);
            definition.name = assetName;
            definition.Id = id;
            definition.NameKey = "weapon." + assetName.ToLowerInvariant();
            definition.Category = category;
            definition.FireMode = fireMode;
            definition.Damage = damage;
            definition.FireInterval = fireInterval;
            definition.Range = range;
            return definition;
        }

        private LoadoutDefinition LoadoutAsset(params LoadoutSlotDefinition[] slots)
        {
            var loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            _created.Add(loadout);
            loadout.name = "TestLoadout";
            loadout.SetSlots(slots);
            loadout.SwitchSeconds = _switchSeconds;
            return loadout;
        }

        private static LoadoutSlotDefinition Slot(WeaponCategory category, WeaponDefinition weapon)
        {
            return new LoadoutSlotDefinition(new[] { category }, weapon);
        }

        /// <summary>0 番が rifle、1 番が handgun のロードアウト。</summary>
        private LoadoutDefinition RifleAndHandgunAsset()
        {
            return LoadoutAsset(Slot(WeaponCategory.AssaultRifle, Rifle()), Slot(WeaponCategory.Handgun, Handgun()));
        }

        private ServerLoadout RifleAndHandgunLoadout()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(RifleAndHandgunAsset(), out var loadout, out var error), Is.True, error);
            return loadout;
        }

        /// <summary>出現したときと同じく、loadout を <see cref="NetPlayerWeapon.ServerEquip"/> で持たせたプレイヤー。</summary>
        private NetPlayerWeapon EquippedWeapon(ServerLoadout loadout)
        {
            var weapon = UnequippedWeapon();
            weapon.ServerEquip(loadout.Weapons, loadout.SwitchSeconds);
            return weapon;
        }

        private NetPlayerWeapon UnequippedWeapon()
        {
            var go = new GameObject("TestNetPlayer");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            return go.AddComponent<NetPlayerWeapon>();
        }

        /// <summary>サーバーと同じく持ち主からの要求 (SwitchWeaponServerRpc) で weaponId に持ち替え、持ち替えが終わるまで時間を進める。</summary>
        private static void SwitchAndWait(NetPlayerWeapon weapon, string weaponId)
        {
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, weaponId), Is.True);
            weapon.ServerTick(_switchSeconds);
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
        }

        private static ShotRejection Fire(ServerLoadout loadout, NetPlayerWeapon weapon, string weaponId, double now, out WeaponSpec spec)
        {
            return NetTeamDeathmatch.CheckWeaponShot(loadout, _shooterId, weapon, weaponId, now, out spec);
        }

        [Test]
        public void MatchTakesTheLoadoutBeforeSpawn()
        {
            var go = new GameObject("TestNetMatch");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            var match = go.AddComponent<NetTeamDeathmatch>();
            var asset = RifleAndHandgunAsset();

            Assert.That(match.Loadout, Is.Null, "a new match has no weapons until the loadout is wired");
            match.Loadout = asset;

            Assert.That(match.Loadout, Is.SameAs(asset));
            Assert.That(match.ServerLoadout, Is.Null, "the loadout is read when the match starts on the server");
        }

        [Test]
        public void LoadsEveryWeaponOfTheLoadoutInSlotOrder()
        {
            var loadout = RifleAndHandgunLoadout();

            Assert.That(loadout.Weapons.Count, Is.EqualTo(2));
            Assert.That(loadout.Weapons[0].Id, Is.EqualTo(_rifleId));
            Assert.That(loadout.Weapons[0].NameKey, Is.EqualTo("weapon.rifle"));
            Assert.That(loadout.Weapons[0].Damage, Is.EqualTo(_rifleDamage));
            Assert.That(loadout.Weapons[1].Id, Is.EqualTo(_handgunId));
            Assert.That(loadout.Weapons[1].Damage, Is.EqualTo(_handgunDamage));
            Assert.That(loadout.SwitchSeconds, Is.EqualTo(_switchSeconds));
            Assert.That(loadout.Rule.SlotCount, Is.EqualTo(2));
            Assert.That(loadout.PlayerCount, Is.Zero, "nobody has a fire rate budget until they shoot");
        }

        [Test]
        public void RefusesToStartWithoutALoadout()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(null, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("no loadout"));
        }

        [Test]
        public void RefusesToStartWithALoadoutWithoutSlots()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(LoadoutAsset(), out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("TestLoadout"));
        }

        [Test]
        public void RefusesToStartWithASlotWithoutAWeapon()
        {
            var asset = LoadoutAsset(Slot(WeaponCategory.AssaultRifle, Rifle()), Slot(WeaponCategory.Handgun, null));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(asset, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("slot #1"));
        }

        [Test]
        public void RefusesToStartWithABrokenWeapon()
        {
            var broken = Rifle();
            broken.name = "Broken";
            broken.MagazineSize = 0;
            var asset = LoadoutAsset(Slot(WeaponCategory.AssaultRifle, broken), Slot(WeaponCategory.Handgun, Handgun()));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(asset, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("Broken"));
        }

        [Test]
        public void RefusesToStartWhenAWeaponDoesNotFitItsSlot()
        {
            var asset = LoadoutAsset(Slot(WeaponCategory.AssaultRifle, Handgun()), Slot(WeaponCategory.Handgun, Rifle()));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(asset, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("does not accept"));
        }

        [Test]
        public void RefusesToStartWithTheSameWeaponInTwoSlots()
        {
            var rifle = Rifle();
            var asset = LoadoutAsset(
                new LoadoutSlotDefinition(new[] { WeaponCategory.AssaultRifle }, rifle),
                new LoadoutSlotDefinition(new[] { WeaponCategory.AssaultRifle }, rifle));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(asset, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("already in slot"));
        }

        [Test]
        public void RefusesToStartWithAnInvalidSwitchTime()
        {
            var asset = RifleAndHandgunAsset();
            asset.SwitchSeconds = float.NaN;

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(asset, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("switch seconds"));
        }

        [Test]
        public void RefusesToStartWithANonUuidId()
        {
            var old = Handgun();
            old.name = "OldHandgun";
            old.Id = "handgun";
            var asset = LoadoutAsset(Slot(WeaponCategory.AssaultRifle, Rifle()), Slot(WeaponCategory.Handgun, old));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(asset, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("OldHandgun"));
        }

        [Test]
        public void DamageFollowsTheHeldWeapon()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);

            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec, Is.SameAs(weapon.CurrentSpec));
            Assert.That(DamageCalculator.Calculate(spec.Damage, HitZone.Body, 10f, spec.Falloff), Is.EqualTo(_rifleDamage));

            SwitchAndWait(weapon, _handgunId);

            Assert.That(Fire(loadout, weapon, _handgunId, 1d, out spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Id, Is.EqualTo(_handgunId));
            Assert.That(DamageCalculator.Calculate(spec.Damage, HitZone.Body, 10f, spec.Falloff), Is.EqualTo(_handgunDamage));
        }

        [Test]
        public void RangeFollowsTheHeldWeapon()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);

            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Range, Is.EqualTo(_rifleRange));

            SwitchAndWait(weapon, _handgunId);

            // ServerFire はこの spec.Range まで Raycast する
            Assert.That(Fire(loadout, weapon, _handgunId, 1d, out spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Range, Is.EqualTo(_handgunRange));
        }

        [Test]
        public void SwitchToAWeaponOutsideTheLoadoutIsDropped()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);

            LogAssert.Expect(LogType.Warning, new Regex("ignored switch client=" + weapon.OwnerClientId + " weaponIdLength=" + _shotgunId.Length + " not in the loadout"));
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, _shotgunId), Is.False);

            Assert.That(weapon.Loadout.IsSwitching, Is.False, "a dropped request does not start the switch delay");
            Assert.That(weapon.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Id, Is.EqualTo(_rifleId));
            Assert.That(Fire(loadout, weapon, _shotgunId, 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
        }

        [Test]
        public void ShotsAreDroppedWhileTheNewWeaponIsDrawn()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, _handgunId), Is.True);

            // 持ち替えた直後に届いた古い Id の射撃は WeaponMismatch。新しい Id でも抜き終わるまでは弾を減らさずに捨てる
            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.Switching));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(weapon.CurrentSpec.MagazineSize));
        }

        [Test]
        public void RespawnHoldsTheRifleWithEveryWeaponFull()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            SwitchAndWait(weapon, _handgunId);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);

            NetTeamDeathmatch.ArmForSpawn(loadout, GameRules.Default, weapon);

            Assert.That(weapon.WeaponId, Is.EqualTo(_rifleId));
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_rifleId));
            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False, "a respawned player can fire at once");
            foreach (var held in weapon.Loadout.Items)
            {
                Assert.That(held.AmmoInMagazine, Is.EqualTo(held.Spec.MagazineSize), held.Spec.Id);
                Assert.That(held.ReserveAmmo, Is.EqualTo(held.Spec.ReserveAmmo), held.Spec.Id);
                Assert.That(held.IsReloading, Is.False, held.Spec.Id);
            }

            // 射撃は rifle の値で判定し、倒れる前に持っていた handgun の Id では撃てない
            Assert.That(Fire(loadout, weapon, _handgunId, 2d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(Fire(loadout, weapon, _rifleId, 2d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec.Damage, Is.EqualTo(_rifleDamage));
            Assert.That(loadout.TryGetBudget(_shooterId, out var budget), Is.True);
            Assert.That(budget.FireInterval, Is.EqualTo(_rifleInterval));
        }

        [Test]
        public void SpawningWithoutAWeaponComponentDoesNothing()
        {
            Assert.DoesNotThrow(() => NetTeamDeathmatch.ArmForSpawn(RifleAndHandgunLoadout(), GameRules.Default, null));
        }

        [Test]
        public void SpawningWithoutRulesThrows()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = UnequippedWeapon();

            Assert.Throws<System.ArgumentNullException>(() => NetTeamDeathmatch.ArmForSpawn(loadout, null, weapon));
            Assert.That(weapon.Loadout, Is.Null, "nothing is equipped when the rules are missing");
        }

        /// <summary>予備弾のルールだけを変え、ほかは <see cref="GameRules.Default"/> と同じルール。</summary>
        private static GameRules ReserveRules(bool infiniteReserve, float reserveAmmoMultiplier)
        {
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                infiniteReserve,
                reserveAmmoMultiplier,
                Destruction.Off);
        }

        /// <summary>出現したときと同じく、loadout を rules で <see cref="NetTeamDeathmatch.ArmForSpawn"/> から持たせたプレイヤー。</summary>
        private NetPlayerWeapon SpawnedWeapon(ServerLoadout loadout, GameRules rules)
        {
            var weapon = UnequippedWeapon();
            NetTeamDeathmatch.ArmForSpawn(loadout, rules, weapon);
            return weapon;
        }

        // WeaponDefinition の既定の予備弾は 90 発
        [TestCase(0.5f, 45)]
        [TestCase(2f, 180)]
        public void SpawnScalesTheReserveOfEveryWeaponByTheRules(float multiplier, int expected)
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = SpawnedWeapon(loadout, ReserveRules(false, multiplier));

            foreach (var held in weapon.Loadout.Items)
            {
                Assert.That(held.Spec.ReserveAmmo, Is.EqualTo(90), held.Spec.Id);
                Assert.That(held.ReserveAmmo, Is.EqualTo(expected), held.Spec.Id);
                Assert.That(held.AmmoInMagazine, Is.EqualTo(held.Spec.MagazineSize), held.Spec.Id);
                Assert.That(held.HasInfiniteReserve, Is.False, held.Spec.Id);
            }

            // リロードで減った予備弾は、試合の開始 (ServerRefill) で倍率を掛けた数に戻る
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
            weapon.ServerTick(weapon.CurrentSpec.ReloadSeconds);
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(expected - 1));

            weapon.ServerRefill();

            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(expected));
        }

        [Test]
        public void InfiniteReserveReloadsFromZeroReserve()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = SpawnedWeapon(loadout, ReserveRules(true, 0f));
            var rifle = weapon.ServerWeapon;
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(0), "a multiplier of 0 leaves no reserve to show");
            Assert.That(rifle.HasInfiniteReserve, Is.True);

            // 手でのリロード (ReloadServerRpc) を、予備弾 0 でもサーバーが受ける
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
            weapon.ServerTick(rifle.Spec.ReloadSeconds);
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(rifle.Spec.MagazineSize));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(0));

            // 弾倉を撃ち切ったときの自動リロードも、予備弾 0 から始まる
            for (var i = 0; i < rifle.Spec.MagazineSize; i++)
            {
                Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None), "shot " + i);
            }

            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(rifle.IsReloading, Is.True);
            weapon.ServerTick(rifle.Spec.ReloadSeconds);
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(rifle.Spec.MagazineSize));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(0));
        }

        [Test]
        public void FiniteReserveAtZeroRefusesTheReload()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = SpawnedWeapon(loadout, ReserveRules(false, 0f));

            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False);
            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(weapon.CurrentSpec.MagazineSize - 1));
        }

        [Test]
        public void ClientSendsTheReloadEvenWithNoReserve()
        {
            // 予備弾 0 でも送り、許すかどうか (無限予備弾か) はサーバーが決める
            Assert.That(NetPlayerWeapon.ShouldRequestReload(new NetWeaponState(0, 0, false, 0, 2, false), true), Is.True);
            Assert.That(NetPlayerWeapon.ShouldRequestReload(new NetWeaponState(29, 90, false, 0, 2, false), true), Is.True);

            Assert.That(NetPlayerWeapon.ShouldRequestReload(new NetWeaponState(0, 0, true, 0, 2, false), true), Is.False, "already reloading");
            Assert.That(NetPlayerWeapon.ShouldRequestReload(new NetWeaponState(0, 0, false, 0, 2, true), true), Is.False, "switching");
            Assert.That(NetPlayerWeapon.ShouldRequestReload(new NetWeaponState(0, 0, false, 0, 2, false), false), Is.False, "down");
        }

        private static GameRules ActiveReloadRules(ActiveReload activeReload)
        {
            var a = ActionRules.Default;
            return new GameRules(
                FriendlyFire.Off,
                RespawnMode.Individual,
                GameRules.DefaultWaveIntervalSeconds,
                0f,
                0f,
                DeathBody.Vanish,
                DeathCamera.None,
                false,
                1f,
                Destruction.Off,
                new ActionRules(a.Downed, a.DownedBleedoutSeconds, a.DownedHealthFraction, a.ReviveSeconds, a.Cover, a.BlindFire, activeReload));
        }

        [TestCase(ActiveReload.On, true)]
        [TestCase(ActiveReload.Off, false)]
        public void SpawnGivesEveryWeaponTheActiveReloadRule(ActiveReload activeReload, bool expected)
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = SpawnedWeapon(loadout, ActiveReloadRules(activeReload));

            foreach (var held in weapon.Loadout.Items)
            {
                Assert.That(held.HasActiveReload, Is.EqualTo(expected), held.Spec.Id);
            }

            // リスポーンで満タンに戻しても同じルールのまま
            weapon.ServerRefill();
            foreach (var held in weapon.Loadout.Items)
            {
                Assert.That(held.HasActiveReload, Is.EqualTo(expected), held.Spec.Id);
            }
        }

        [Test]
        public void SpawnedWeaponIsJudgedOnTheServerWithTheMatchRules()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = SpawnedWeapon(loadout, ActiveReloadRules(ActiveReload.On));
            var spec = weapon.CurrentSpec;
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
            weapon.ServerTick(spec.ReloadSeconds * (spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength / 2f));

            LogAssert.Expect(LogType.Log, NetPlayerWeapon.ActiveReloadLog(weapon.OwnerClientId, ActiveReloadResult.Success));
            Assert.That(weapon.ServerHandleActiveReloadRequest(weapon.OwnerClientId), Is.True);

            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(spec.MagazineSize));
        }

        [Test]
        public void SpawnedWeaponWithTheOffRuleKeepsTheUsualReload()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = SpawnedWeapon(loadout, ActiveReloadRules(ActiveReload.Off));
            var spec = weapon.CurrentSpec;
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
            var inTheWindow = spec.ReloadSeconds * (spec.ActiveReloadWindowStart + spec.ActiveReloadWindowLength / 2f);
            weapon.ServerTick(inTheWindow);

            Assert.That(weapon.ServerHandleActiveReloadRequest(weapon.OwnerClientId), Is.False);
            Assert.That(weapon.ServerWeapon.IsReloading, Is.True);

            weapon.ServerTick(spec.ReloadSeconds - inTheWindow);
            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(spec.MagazineSize));
        }

        [Test]
        public void ShotWithTheWeaponHeldBeforeASwitchIsDropped()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);

            Assert.That(Fire(loadout, weapon, _handgunId, 0d, out var spec), Is.EqualTo(ShotRejection.WeaponMismatch), "the handgun is not drawn yet");
            Assert.That(spec, Is.Null);

            SwitchAndWait(weapon, _handgunId);

            // 持ち替えた後に届いた rifle の射撃は、rifle の値で判定しない
            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out spec), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(spec, Is.Null);
        }

        [Test]
        public void FireRateFollowsTheWeaponAfterASwitch()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);
            for (var i = 0; i < ShotBudget.DefaultBurst; i++)
            {
                Assert.That(Fire(loadout, weapon, _rifleId, 0d, out _), Is.EqualTo(ShotRejection.None), "rifle shot " + i);
            }

            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out _), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(loadout.TryGetBudget(_shooterId, out var budget), Is.True);
            Assert.That(budget.FireInterval, Is.EqualTo(_rifleInterval));

            SwitchAndWait(weapon, _handgunId);

            // 持ち替えたら handgun の間隔で作り直すので、溜めは満タンから
            for (var i = 0; i < ShotBudget.DefaultBurst; i++)
            {
                Assert.That(Fire(loadout, weapon, _handgunId, 1d, out _), Is.EqualTo(ShotRejection.None), "handgun shot " + i);
            }

            Assert.That(loadout.TryGetBudget(_shooterId, out budget), Is.True);
            Assert.That(budget.FireInterval, Is.EqualTo(_handgunInterval));

            // rifle の間隔 (0.1 秒) なら 1 発ぶん戻るが、handgun (0.3 秒) では戻らない
            Assert.That(Fire(loadout, weapon, _handgunId, 1.1d, out _), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(Fire(loadout, weapon, _handgunId, 1.35d, out _), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ShotWithAnUnknownWeaponIdIsDropped()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);

            Assert.That(Fire(loadout, weapon, _shotgunId, 0d, out var spec), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(spec, Is.Null);
            Assert.That(Fire(loadout, weapon, string.Empty, 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(Fire(loadout, weapon, null, 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(Fire(loadout, weapon, _rifleId.ToUpperInvariant(), 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
        }

        [Test]
        public void ShotBeforeTheWeaponIsGivenIsDropped()
        {
            var loadout = RifleAndHandgunLoadout();

            Assert.That(Fire(loadout, UnequippedWeapon(), _rifleId, 0d, out var spec), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(spec, Is.Null);
            Assert.That(Fire(loadout, null, _rifleId, 0d, out spec), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(spec, Is.Null);
        }

        [Test]
        public void DroppedShotsDoNotUseUpTheFireRate()
        {
            var loadout = RifleAndHandgunLoadout();
            var weapon = EquippedWeapon(loadout);

            for (var i = 0; i < 10; i++)
            {
                Assert.That(Fire(loadout, weapon, _handgunId, 0d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
                Assert.That(Fire(loadout, weapon, _shotgunId, 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
            }

            // 武器が正しい射撃だけが rifle の連射間隔 (ShotBudget) を減らす
            for (var i = 0; i < ShotBudget.DefaultBurst; i++)
            {
                Assert.That(Fire(loadout, weapon, _rifleId, 0d, out _), Is.EqualTo(ShotRejection.None), "shot " + i);
            }

            Assert.That(Fire(loadout, weapon, _rifleId, 0d, out _), Is.EqualTo(ShotRejection.FireRateExceeded));
        }

        [Test]
        public void RejectedWeaponShotsUseTheSameWarning()
        {
            Assert.That(NetHitLog.Rejected(3, ShotRejection.UnknownWeapon), Is.EqualTo("[Net] rejected shot attacker=3 reason=UnknownWeapon"));
            Assert.That(NetHitLog.Rejected(3, ShotRejection.WeaponMismatch), Is.EqualTo("[Net] rejected shot attacker=3 reason=WeaponMismatch"));
        }
    }
}

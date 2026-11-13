using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// サーバー権威の弾倉・リロード・持ち替え (<see cref="NetPlayerWeapon"/>)。Spawn していないプレイヤーで、サーバー側の判断だけを確かめる
    /// (同期は Spawn してから行うので、ここでは <see cref="NetPlayerWeapon.ServerWeapon"/> と <see cref="NetPlayerWeapon.Loadout"/> を読む)。
    /// </summary>
    public sealed class NetPlayerWeaponTests
    {
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const float _switchSeconds = 0.5f;

        private readonly List<GameObject> _created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _created)
            {
                if (go != null)
                {
                    Object.DestroyImmediate(go);
                }
            }

            _created.Clear();
        }

        // ネット対戦と同じく、武器の値は定義アセット (WeaponDefinition の既定 = rifle) から引く
        private static WeaponSpec DefaultSpec()
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            try
            {
                definition.Id = WeaponAssetBuilder.RifleId;
                return definition.ToSpec();
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private static WeaponSpec HandgunSpec()
        {
            return new WeaponSpec(_handgunId, null, WeaponCategory.Handgun, FireMode.Semi, 5, 10, 0.1f, 1f, 25, 50f, DamageFalloff.None);
        }

        private NetPlayerWeapon CreateUnequipped()
        {
            var go = new GameObject("TestNetPlayer");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            return go.AddComponent<NetPlayerWeapon>();
        }

        /// <summary>rifle 1 本だけの武器。</summary>
        private NetPlayerWeapon CreateWeapon()
        {
            var weapon = CreateUnequipped();
            weapon.ServerEquip(new[] { DefaultSpec() }, _switchSeconds);
            return weapon;
        }

        /// <summary>0 番が rifle、1 番が handgun の武器。</summary>
        private NetPlayerWeapon CreateTwoWeapons()
        {
            var weapon = CreateUnequipped();
            weapon.ServerEquip(new[] { DefaultSpec(), HandgunSpec() }, _switchSeconds);
            return weapon;
        }

        private static void EmptyMagazine(NetPlayerWeapon weapon)
        {
            for (var i = 0; i < weapon.ServerWeapon.Spec.MagazineSize; i++)
            {
                Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None), "shot " + i);
            }
        }

        private static string RifleId => WeaponAssetBuilder.RifleId;

        // CreateTwoWeapons の並び (0 番が rifle、1 番が handgun) での番号
        private static int IndexOf(string weaponId)
        {
            return weaponId == _handgunId ? 1 : 0;
        }

        private static void ExpectSwitchLog(NetPlayerWeapon weapon, string weaponId)
        {
            LogAssert.Expect(LogType.Log, "[Net] switch client=" + weapon.OwnerClientId + " index=" + IndexOf(weaponId) + " weapon=" + weaponId);
        }

        /// <summary>持ち主からの要求で weaponId に持ち替え、持ち替えが終わるまで時間を進める。</summary>
        private static void SwitchAndWait(NetPlayerWeapon weapon, string weaponId)
        {
            ExpectSwitchLog(weapon, weaponId);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, weaponId), Is.True);
            weapon.ServerTick(_switchSeconds);
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
        }

        [Test]
        public void DefaultSpecIsTheRifleOfTheOfflineWeapon()
        {
            // 以下のテストの弾数は、オフラインの WeaponController と同じ rifle (30 発 / 予備 90 / リロード 2 秒) を前提にする
            var spec = DefaultSpec();
            Assert.That(spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(spec.Category, Is.EqualTo(WeaponCategory.AssaultRifle));
            Assert.That(spec.MagazineSize, Is.EqualTo(30));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(90));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void EquipSetsTheWeaponIdSentWithShots()
        {
            var weapon = CreateUnequipped();
            Assert.That(weapon.WeaponId, Is.Empty, "no weapon id is sent before the server equips one");
            Assert.That(weapon.CurrentSpec, Is.Null);

            weapon.ServerEquip(new[] { DefaultSpec() }, _switchSeconds);
            Assert.That(weapon.WeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));

            weapon.ServerEquip(new[] { HandgunSpec() }, _switchSeconds);
            Assert.That(weapon.WeaponId, Is.EqualTo(_handgunId));
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_handgunId));
        }

        [Test]
        public void UuidWeaponIdsFitTheShotRpc()
        {
            Assert.That(NetPlayerWeapon.FitsWeaponId(WeaponAssetBuilder.RifleId), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(WeaponAssetBuilder.RifleId), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(WeaponAssetBuilder.HandgunId), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(new string('a', NetPlayerWeapon.MaxWeaponIdBytes)), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(new string('a', NetPlayerWeapon.MaxWeaponIdBytes + 1)), Is.False);
            Assert.That(NetPlayerWeapon.FitsWeaponId(null), Is.False);
        }

        [Test]
        public void EquipGivesFullWeaponsInOrderHoldingTheFirst()
        {
            var weapon = CreateTwoWeapons();

            Assert.That(weapon.Loadout, Is.Not.Null);
            Assert.That(weapon.Loadout.Count, Is.EqualTo(2));
            Assert.That(weapon.Loadout.SwitchSeconds, Is.EqualTo(_switchSeconds));
            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
            Assert.That(weapon.Loadout.Items[0].Spec.Id, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(weapon.Loadout.Items[1].Spec.Id, Is.EqualTo(_handgunId));
            Assert.That(weapon.ServerWeapon, Is.SameAs(weapon.Loadout.Items[0]));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.Loadout.Items[1].ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void EquipRejectsInvalidLoadoutsAndKeepsThePreviousWeapons()
        {
            var weapon = CreateWeapon();
            var previous = weapon.Loadout;

            Assert.Throws<ArgumentNullException>(() => weapon.ServerEquip(null, _switchSeconds));
            Assert.Throws<ArgumentException>(() => weapon.ServerEquip(Array.Empty<WeaponSpec>(), _switchSeconds));
            Assert.Throws<ArgumentException>(() => weapon.ServerEquip(new[] { DefaultSpec(), null }, _switchSeconds));
            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.ServerEquip(new[] { HandgunSpec() }, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => weapon.ServerEquip(new[] { HandgunSpec() }, float.NaN));

            var tooMany = new WeaponSpec[NetPlayerWeapon.MaxWeaponCount + 1];
            for (var i = 0; i < tooMany.Length; i++)
            {
                tooMany[i] = HandgunSpec();
            }

            Assert.Throws<ArgumentException>(() => weapon.ServerEquip(tooMany, _switchSeconds));

            Assert.That(weapon.Loadout, Is.SameAs(previous));
            Assert.That(weapon.WeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));
        }

        [Test]
        public void RejectsShotsBeforeEquip()
        {
            var weapon = CreateUnequipped();

            // ServerFire は None 以外ならダメージを与えずに捨てる
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, RifleId), Is.False);
            Assert.DoesNotThrow(weapon.ServerRefill);
            Assert.DoesNotThrow(() => weapon.ServerTick(1f));
        }

        [Test]
        public void EmptyMagazineRejectsShotsSoNoDamageIsDealt()
        {
            var weapon = CreateWeapon();
            EmptyMagazine(weapon);

            // ServerFire は None 以外ならダメージを与えずに捨てる。空撃ちでサーバーがリロードを始める
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.OutOfAmmo));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(0));
            Assert.That(weapon.ServerWeapon.IsReloading, Is.True);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.Reloading));
        }

        [Test]
        public void CanFireAfterTheAutomaticReload()
        {
            var weapon = CreateWeapon();
            EmptyMagazine(weapon);
            weapon.ServerTryFire();

            weapon.ServerTick(weapon.ServerWeapon.Spec.ReloadSeconds);

            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(60));
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(29));
        }

        [Test]
        public void OwnerReloadRequestStartsTheReloadAndBlocksShots()
        {
            var weapon = CreateWeapon();
            weapon.ServerTryFire();

            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
            Assert.That(weapon.ServerWeapon.IsReloading, Is.True);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.Reloading));

            weapon.ServerTick(weapon.ServerWeapon.Spec.ReloadSeconds);

            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(89));
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void ReloadRequestFromAnotherClientIsIgnored()
        {
            var weapon = CreateWeapon();
            weapon.ServerTryFire();
            var intruder = weapon.OwnerClientId + 1;

            LogAssert.Expect(LogType.Warning, new Regex("ignored reload from client " + intruder));
            Assert.That(weapon.ServerHandleReloadRequest(intruder), Is.False);

            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(29));
        }

        [Test]
        public void SwitchRequestChangesTheCurrentWeaponAfterTheSwitchTime()
        {
            var weapon = CreateTwoWeapons();

            ExpectSwitchLog(weapon, _handgunId);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, _handgunId), Is.True);

            // 持ち替え中も、送る Id とダメージに使う Spec は持ち替え先の武器
            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(1));
            Assert.That(weapon.Loadout.IsSwitching, Is.True);
            Assert.That(weapon.WeaponId, Is.EqualTo(_handgunId));
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_handgunId));
            Assert.That(weapon.ServerWeapon, Is.SameAs(weapon.Loadout.Items[1]));

            weapon.ServerTick(_switchSeconds * 0.5f);
            Assert.That(weapon.Loadout.IsSwitching, Is.True);

            weapon.ServerTick(_switchSeconds * 0.5f);
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(4));
        }

        [Test]
        public void ShotsAndReloadsAreRejectedWhileSwitching()
        {
            var weapon = CreateTwoWeapons();
            weapon.ServerTryFire();

            ExpectSwitchLog(weapon, _handgunId);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, _handgunId);

            // ServerFire は None 以外ならダメージを与えずに捨てる。弾も減らさない
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.Switching));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False);

            ExpectSwitchLog(weapon, RifleId);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, RifleId);
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False, "rifle has 29 rounds but is still being drawn");
            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);

            weapon.ServerTick(_switchSeconds);
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
        }

        [Test]
        public void SwitchingKeepsTheAmmoOfEachWeapon()
        {
            var weapon = CreateTwoWeapons();
            for (var i = 0; i < 3; i++)
            {
                weapon.ServerTryFire();
            }

            SwitchAndWait(weapon, _handgunId);
            weapon.ServerTryFire();
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(4));

            SwitchAndWait(weapon, RifleId);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(27));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(4));
            Assert.That(weapon.Loadout.Items[1].ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void SwitchingCancelsTheReloadOfTheHolsteredWeapon()
        {
            var weapon = CreateTwoWeapons();
            var rifle = weapon.ServerWeapon;
            weapon.ServerTryFire();
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);

            SwitchAndWait(weapon, _handgunId);
            Assert.That(rifle.IsReloading, Is.False);

            // しまった武器は Tick しないので、リロードの時間が過ぎても補充されない
            weapon.ServerTick(rifle.Spec.ReloadSeconds);
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(29));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));
        }

        [Test]
        public void SwitchRequestsOutsideTheLoadoutOrFromAnotherClientAreIgnored()
        {
            var weapon = CreateTwoWeapons();
            var rifle = weapon.ServerWeapon;
            weapon.ServerTryFire();
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);
            var intruder = weapon.OwnerClientId + 1;
            const string shotgunId = "00000000-0000-4000-8000-000000000003";

            foreach (var id in new[] { shotgunId, _handgunId.ToUpperInvariant(), string.Empty, null })
            {
                LogAssert.Expect(LogType.Warning, new Regex("ignored switch client=" + weapon.OwnerClientId + " weapon='"
                    + Regex.Escape(id ?? string.Empty) + "' not in the loadout"));
                Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, id), Is.False, "id '" + id + "'");
            }

            LogAssert.Expect(LogType.Warning, new Regex("ignored switch from client " + intruder));
            Assert.That(weapon.ServerHandleSwitchRequest(intruder, _handgunId), Is.False);

            // 今の武器と同じなら持ち替えない (持ち替えの待ち時間も始めない)
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, RifleId), Is.False);

            // 捨てた要求では持ち替えず、リロードも止まらない
            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
            Assert.That(weapon.ServerWeapon, Is.SameAs(rifle));
            Assert.That(weapon.WeaponId, Is.EqualTo(RifleId));
            Assert.That(rifle.IsReloading, Is.True);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SingleWeaponCannotSwitch()
        {
            var weapon = CreateWeapon();

            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, RifleId), Is.False);
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
        }

        [Test]
        public void NeighbourWeaponWrapsAroundTheLoadout()
        {
            var specs = new[] { DefaultSpec(), HandgunSpec() };

            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, RifleId, 1), Is.EqualTo(_handgunId));
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, _handgunId, 1), Is.EqualTo(RifleId));
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, RifleId, -1), Is.EqualTo(_handgunId));
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, _handgunId, -1), Is.EqualTo(RifleId));
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, RifleId, 3), Is.EqualTo(_handgunId));
        }

        [Test]
        public void NoNeighbourWeaponWithoutAnotherWeaponOrTheCurrentOne()
        {
            var specs = new[] { DefaultSpec(), HandgunSpec() };

            Assert.That(NetPlayerWeapon.NeighbourWeaponId(new[] { DefaultSpec() }, RifleId, 1), Is.Null);
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(null, RifleId, 1), Is.Null);
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, string.Empty, 1), Is.Null, "before the server equips a weapon");
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, null, 1), Is.Null);
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, "00000000-0000-4000-8000-000000000003", 1), Is.Null);
            Assert.That(NetPlayerWeapon.NeighbourWeaponId(specs, RifleId, 0), Is.Null);
        }

        [Test]
        public void RefillOnRespawnRestoresFullAmmoAndCancelsTheReload()
        {
            var weapon = CreateWeapon();
            EmptyMagazine(weapon);
            weapon.ServerTryFire();
            weapon.ServerTick(weapon.ServerWeapon.Spec.ReloadSeconds);
            EmptyMagazine(weapon);
            weapon.ServerTryFire();
            Assert.That(weapon.ServerWeapon.IsReloading, Is.True);
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(60));

            weapon.ServerRefill();

            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RefillRestoresEveryWeaponAndHoldsTheFirstWithoutSwitching()
        {
            var weapon = CreateTwoWeapons();
            var rifle = weapon.ServerWeapon;
            weapon.ServerTryFire();
            SwitchAndWait(weapon, _handgunId);
            weapon.ServerTryFire();
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);

            weapon.ServerRefill();

            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False, "a respawned player can fire at once");
            Assert.That(weapon.ServerWeapon, Is.SameAs(rifle));
            Assert.That(weapon.WeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.Loadout.Items[1].ReserveAmmo, Is.EqualTo(10));
            Assert.That(weapon.Loadout.Items[1].IsReloading, Is.False);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));

            // 作り直した並びでも持ち替えとリロードの購読が続いている
            SwitchAndWait(weapon, _handgunId);
            Assert.That(weapon.WeaponId, Is.EqualTo(_handgunId));
        }

        [Test]
        public void RefillWhileSwitchingEndsTheSwitch()
        {
            var weapon = CreateTwoWeapons();
            ExpectSwitchLog(weapon, _handgunId);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, _handgunId);

            weapon.ServerRefill();

            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void EquipAgainReplacesTheWeapons()
        {
            var weapon = CreateTwoWeapons();
            weapon.ServerTryFire();
            ExpectSwitchLog(weapon, _handgunId);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, _handgunId);

            weapon.ServerEquip(new[] { HandgunSpec() }, 0f);

            Assert.That(weapon.Loadout.Count, Is.EqualTo(1));
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void ClientSideChecksDoNothingBeforeSpawn()
        {
            var weapon = CreateTwoWeapons();

            // 同期された状態はまだ届いていない (既定値) ので、撃たずにリロードも持ち替えも送らない
            Assert.That(weapon.State, Is.EqualTo(default(NetWeaponState)));
            Assert.That(weapon.PrepareFire(), Is.False);
            Assert.That(weapon.RequestReload(), Is.False);
            Assert.That(weapon.RequestSwitch(_handgunId), Is.False);
            Assert.That(weapon.RequestNextWeapon(), Is.False);
            Assert.That(weapon.RequestPreviousWeapon(), Is.False);
        }

        [Test]
        public void SwitchActionsAreTheProjectInputActions()
        {
            Assert.That(NetPlayerWeapon.NextActionPath, Is.EqualTo("Player/Next"));
            Assert.That(NetPlayerWeapon.PreviousActionPath, Is.EqualTo("Player/Previous"));
        }

        [Test]
        public void WeaponStateEqualityUsesAllFields()
        {
            var state = new NetWeaponState(12, 60, false, 1, 2, false);

            Assert.That(state.Equals(new NetWeaponState(12, 60, false, 1, 2, false)), Is.True);
            Assert.That(state.GetHashCode(), Is.EqualTo(new NetWeaponState(12, 60, false, 1, 2, false).GetHashCode()));
            Assert.That(state.Equals(new NetWeaponState(11, 60, false, 1, 2, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 59, false, 1, 2, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 60, true, 1, 2, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 60, false, 0, 2, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 60, false, 1, 3, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 60, false, 1, 2, true)), Is.False);
        }
    }
}

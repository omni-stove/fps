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
                definition.Id = NetTeamDeathmatch.DefaultWeaponId;
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

        /// <summary>ネット対戦が今渡しているのと同じ、rifle 1 本だけの武器。</summary>
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

        private static void ExpectSwitchLog(NetPlayerWeapon weapon, int index)
        {
            LogAssert.Expect(LogType.Log, "[Net] switch client=" + weapon.OwnerClientId + " index=" + index);
        }

        /// <summary>index に持ち替え、持ち替えが終わるまで時間を進める。</summary>
        private static void SwitchAndWait(NetPlayerWeapon weapon, int index)
        {
            ExpectSwitchLog(weapon, index);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, index), Is.True);
            weapon.ServerTick(_switchSeconds);
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
        }

        [Test]
        public void DefaultSpecIsTheRifleOfTheOfflineWeapon()
        {
            // 以下のテストの弾数は、オフラインの WeaponController と同じ rifle (30 発 / 予備 90 / リロード 2 秒) を前提にする
            var spec = DefaultSpec();
            Assert.That(spec.Id, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));
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
            Assert.That(weapon.WeaponId, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));

            weapon.ServerEquip(new[] { HandgunSpec() }, _switchSeconds);
            Assert.That(weapon.WeaponId, Is.EqualTo(_handgunId));
            Assert.That(weapon.CurrentSpec.Id, Is.EqualTo(_handgunId));
        }

        [Test]
        public void UuidWeaponIdsFitTheShotRpc()
        {
            Assert.That(NetPlayerWeapon.FitsWeaponId(NetTeamDeathmatch.DefaultWeaponId), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(WeaponAssetBuilder.RifleId), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(WeaponAssetBuilder.HandgunId), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(new string('a', NetPlayerWeapon.MaxWeaponIdBytes)), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(new string('a', NetPlayerWeapon.MaxWeaponIdBytes + 1)), Is.False);
            Assert.That(NetPlayerWeapon.FitsWeaponId(null), Is.False);
        }

        [Test]
        public void DefaultWeaponIdIsTheRifleOfTheWeaponAssets()
        {
            // DefaultWeaponId は Fps.Net から Fps.Editor を参照できないのでリテラルで持つ。ずれたら試合が始まらない
            Assert.That(NetTeamDeathmatch.DefaultWeaponId, Is.EqualTo(WeaponAssetBuilder.RifleId));
            Assert.That(WeaponAssetBuilder.RifleSpec().Category, Is.EqualTo(WeaponCategory.AssaultRifle));
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
            Assert.That(weapon.Loadout.Items[0].Spec.Id, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));
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
            Assert.That(weapon.WeaponId, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));
        }

        [Test]
        public void RejectsShotsBeforeEquip()
        {
            var weapon = CreateUnequipped();

            // ServerFire は None 以外ならダメージを与えずに捨てる
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 0), Is.False);
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

            ExpectSwitchLog(weapon, 1);
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 1), Is.True);

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

            ExpectSwitchLog(weapon, 1);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 1);

            // ServerFire は None 以外ならダメージを与えずに捨てる。弾も減らさない
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.Switching));
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False);

            ExpectSwitchLog(weapon, 0);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 0);
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

            SwitchAndWait(weapon, 1);
            weapon.ServerTryFire();
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(4));

            SwitchAndWait(weapon, 0);
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

            SwitchAndWait(weapon, 1);
            Assert.That(rifle.IsReloading, Is.False);

            // しまった武器は Tick しないので、リロードの時間が過ぎても補充されない
            weapon.ServerTick(rifle.Spec.ReloadSeconds);
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(29));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));
        }

        [Test]
        public void SwitchRequestsOutOfRangeOrFromAnotherClientAreIgnored()
        {
            var weapon = CreateTwoWeapons();
            var intruder = weapon.OwnerClientId + 1;

            LogAssert.Expect(LogType.Warning, new Regex("ignored switch client=" + weapon.OwnerClientId + " index=2 count=2"));
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 2), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("ignored switch client=" + weapon.OwnerClientId + " index=-1 count=2"));
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, -1), Is.False);
            LogAssert.Expect(LogType.Warning, new Regex("ignored switch from client " + intruder));
            Assert.That(weapon.ServerHandleSwitchRequest(intruder, 1), Is.False);

            // 今の番号と同じなら持ち替えない (持ち替えの待ち時間も始めない)
            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 0), Is.False);

            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SingleWeaponCannotSwitch()
        {
            var weapon = CreateWeapon();

            Assert.That(weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 0), Is.False);
            Assert.That(weapon.Loadout.IsSwitching, Is.False);
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
            SwitchAndWait(weapon, 1);
            weapon.ServerTryFire();
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.True);

            weapon.ServerRefill();

            Assert.That(weapon.Loadout.CurrentIndex, Is.EqualTo(0));
            Assert.That(weapon.Loadout.IsSwitching, Is.False, "a respawned player can fire at once");
            Assert.That(weapon.ServerWeapon, Is.SameAs(rifle));
            Assert.That(weapon.WeaponId, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));
            Assert.That(rifle.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(rifle.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.Loadout.Items[1].AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.Loadout.Items[1].ReserveAmmo, Is.EqualTo(10));
            Assert.That(weapon.Loadout.Items[1].IsReloading, Is.False);
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None));

            // 作り直した並びでも持ち替えとリロードの購読が続いている
            SwitchAndWait(weapon, 1);
            Assert.That(weapon.WeaponId, Is.EqualTo(_handgunId));
        }

        [Test]
        public void RefillWhileSwitchingEndsTheSwitch()
        {
            var weapon = CreateTwoWeapons();
            ExpectSwitchLog(weapon, 1);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 1);

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
            ExpectSwitchLog(weapon, 1);
            weapon.ServerHandleSwitchRequest(weapon.OwnerClientId, 1);

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
            Assert.That(weapon.RequestSwitch(1), Is.False);
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

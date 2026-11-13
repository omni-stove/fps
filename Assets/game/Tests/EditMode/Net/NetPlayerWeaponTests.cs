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
    /// サーバー権威の弾倉とリロード (<see cref="NetPlayerWeapon"/>)。Spawn していないプレイヤーで、サーバー側の判断だけを確かめる
    /// (同期は Spawn してから行うので、ここでは <see cref="NetPlayerWeapon.ServerWeapon"/> を読む)。
    /// </summary>
    public sealed class NetPlayerWeaponTests
    {
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
                return definition.ToSpec();
            }
            finally
            {
                Object.DestroyImmediate(definition);
            }
        }

        private NetPlayerWeapon CreateWeapon()
        {
            var go = new GameObject("TestNetPlayer");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            var weapon = go.AddComponent<NetPlayerWeapon>();
            weapon.ServerEquip(DefaultSpec());
            return weapon;
        }

        private static void EmptyMagazine(NetPlayerWeapon weapon)
        {
            for (var i = 0; i < weapon.ServerWeapon.Spec.MagazineSize; i++)
            {
                Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None), "shot " + i);
            }
        }

        [Test]
        public void DefaultSpecIsTheRifleOfTheOfflineWeapon()
        {
            // 以下のテストの弾数は、オフラインの WeaponController と同じ rifle (30 発 / 予備 90 / リロード 2 秒) を前提にする
            var spec = DefaultSpec();
            Assert.That(spec.Id, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));
            Assert.That(spec.MagazineSize, Is.EqualTo(30));
            Assert.That(spec.ReserveAmmo, Is.EqualTo(90));
            Assert.That(spec.ReloadSeconds, Is.EqualTo(2f));
        }

        [Test]
        public void EquipSetsTheWeaponIdSentWithShots()
        {
            var go = new GameObject("Unequipped");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            var weapon = go.AddComponent<NetPlayerWeapon>();
            Assert.That(weapon.WeaponId, Is.Empty, "no weapon id is sent before the server equips one");

            weapon.ServerEquip(DefaultSpec());
            Assert.That(weapon.WeaponId, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId));

            weapon.ServerEquip(new WeaponSpec("handgun", null, FireMode.Semi, 5, 10, 0.1f, 1f, 25, 50f, DamageFalloff.None));
            Assert.That(weapon.WeaponId, Is.EqualTo("handgun"));
        }

        [Test]
        public void RejectsAWeaponIdTooLongToSend()
        {
            var weapon = CreateWeapon();
            var longId = new string('a', 30);
            Assert.That(NetPlayerWeapon.FitsWeaponId(new string('a', 29)), Is.True);
            Assert.That(NetPlayerWeapon.FitsWeaponId(longId), Is.False);
            Assert.That(NetPlayerWeapon.FitsWeaponId(null), Is.False);

            Assert.Throws<System.ArgumentException>(
                () => weapon.ServerEquip(new WeaponSpec(longId, null, FireMode.Auto, 5, 10, 0.1f, 1f, 25, 50f, DamageFalloff.None)));
            Assert.That(weapon.WeaponId, Is.EqualTo(NetTeamDeathmatch.DefaultWeaponId), "the previous weapon is kept");
        }

        [Test]
        public void EquipGivesAFullWeapon()
        {
            var weapon = CreateWeapon();

            Assert.That(weapon.ServerWeapon, Is.Not.Null);
            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(30));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(90));
            Assert.That(weapon.ServerWeapon.IsReloading, Is.False);
        }

        [Test]
        public void RejectsShotsBeforeEquip()
        {
            var go = new GameObject("Unequipped");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            var weapon = go.AddComponent<NetPlayerWeapon>();

            // ServerFire は None 以外ならダメージを与えずに捨てる
            Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.NoWeapon));
            Assert.That(weapon.ServerHandleReloadRequest(weapon.OwnerClientId), Is.False);
            Assert.DoesNotThrow(weapon.ServerRefill);
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

            weapon.ServerWeapon.Tick(weapon.ServerWeapon.Spec.ReloadSeconds);

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

            weapon.ServerWeapon.Tick(weapon.ServerWeapon.Spec.ReloadSeconds);

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
        public void RefillOnRespawnRestoresFullAmmoAndCancelsTheReload()
        {
            var weapon = CreateWeapon();
            EmptyMagazine(weapon);
            weapon.ServerTryFire();
            weapon.ServerWeapon.Tick(weapon.ServerWeapon.Spec.ReloadSeconds);
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
        public void EquipAgainReplacesTheWeapon()
        {
            var weapon = CreateWeapon();
            weapon.ServerTryFire();

            weapon.ServerEquip(new WeaponSpec("handgun", null, FireMode.Semi, 5, 10, 0.1f, 1f, 25, 50f, DamageFalloff.None));

            Assert.That(weapon.ServerWeapon.AmmoInMagazine, Is.EqualTo(5));
            Assert.That(weapon.ServerWeapon.ReserveAmmo, Is.EqualTo(10));
        }

        [Test]
        public void ClientSideChecksDoNothingBeforeSpawn()
        {
            var weapon = CreateWeapon();

            // 同期された状態はまだ届いていない (既定値) ので、撃たずにリロードも送らない
            Assert.That(weapon.State, Is.EqualTo(default(NetWeaponState)));
            Assert.That(weapon.PrepareFire(), Is.False);
            Assert.That(weapon.RequestReload(), Is.False);
        }

        [Test]
        public void WeaponStateEqualityUsesAllFields()
        {
            var state = new NetWeaponState(12, 60, false);

            Assert.That(state.Equals(new NetWeaponState(12, 60, false)), Is.True);
            Assert.That(state.GetHashCode(), Is.EqualTo(new NetWeaponState(12, 60, false).GetHashCode()));
            Assert.That(state.Equals(new NetWeaponState(11, 60, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 59, false)), Is.False);
            Assert.That(state.Equals(new NetWeaponState(12, 60, true)), Is.False);
        }
    }
}

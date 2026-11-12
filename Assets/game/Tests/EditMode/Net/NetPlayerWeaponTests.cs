using System.Collections.Generic;
using System.Text.RegularExpressions;
using Fps.Core;
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

        private static WeaponSpec DefaultSpec()
        {
            return new WeaponSpec(
                NetPlayerWeapon.DefaultMagazineSize,
                NetPlayerWeapon.DefaultReserveAmmo,
                NetTeamDeathmatch.DefaultFireInterval,
                NetPlayerWeapon.DefaultReloadSeconds,
                NetTeamDeathmatch.DefaultWeaponDamage);
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
            for (var i = 0; i < NetPlayerWeapon.DefaultMagazineSize; i++)
            {
                Assert.That(weapon.ServerTryFire(), Is.EqualTo(ShotRejection.None), "shot " + i);
            }
        }

        [Test]
        public void DefaultsMatchTheOfflineWeapon()
        {
            // オフラインの WeaponController (Fps.Runtime) と同じ 30 発 / 予備 90 / 連射間隔 0.1 秒 / リロード 2 秒
            Assert.That(NetPlayerWeapon.DefaultMagazineSize, Is.EqualTo(30));
            Assert.That(NetPlayerWeapon.DefaultReserveAmmo, Is.EqualTo(90));
            Assert.That(NetTeamDeathmatch.DefaultFireInterval, Is.EqualTo(0.1f));
            Assert.That(NetPlayerWeapon.DefaultReloadSeconds, Is.EqualTo(2f));
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

            weapon.ServerWeapon.Tick(NetPlayerWeapon.DefaultReloadSeconds);

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

            weapon.ServerWeapon.Tick(NetPlayerWeapon.DefaultReloadSeconds);

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
            weapon.ServerWeapon.Tick(NetPlayerWeapon.DefaultReloadSeconds);
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

            weapon.ServerEquip(new WeaponSpec(5, 10, 0.1f, 1f, 25));

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

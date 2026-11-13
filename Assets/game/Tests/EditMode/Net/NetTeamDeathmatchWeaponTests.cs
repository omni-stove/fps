using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// ネット対戦の武器が <see cref="WeaponCatalogAsset"/> から引かれ、射撃に載った武器の Id で検査されるか。
    /// 試合の開始時に <see cref="NetTeamDeathmatch"/> が使うのと同じ経路 (<see cref="NetTeamDeathmatch.TryCreateLoadout"/>) で確かめる。
    /// </summary>
    public sealed class NetTeamDeathmatchWeaponTests
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

        private WeaponDefinition Definition(string assetName, string id, int damage = 25, float fireInterval = 0.1f)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(definition);
            definition.name = assetName;
            definition.Id = id;
            definition.Damage = damage;
            definition.FireInterval = fireInterval;
            return definition;
        }

        private WeaponCatalogAsset Catalog(params WeaponDefinition[] weapons)
        {
            var catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            _created.Add(catalog);
            catalog.name = "TestWeaponCatalog";
            catalog.SetWeapons(weapons);
            return catalog;
        }

        private ServerLoadout RifleAndHandgunLoadout()
        {
            var catalog = Catalog(
                Definition("Rifle", NetTeamDeathmatch.DefaultWeaponId, damage: 25, fireInterval: 0.1f),
                Definition("Handgun", "handgun", damage: 34, fireInterval: 0.2f));
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(catalog, out var loadout, out var error), Is.True, error);
            return loadout;
        }

        [Test]
        public void MatchTakesTheCatalogBeforeSpawn()
        {
            var go = new GameObject("TestNetMatch");
            _created.Add(go);
            go.AddComponent<NetworkObject>();
            var match = go.AddComponent<NetTeamDeathmatch>();
            var catalog = Catalog(Definition("Rifle", NetTeamDeathmatch.DefaultWeaponId));

            Assert.That(match.Catalog, Is.Null, "a new match has no weapons until the catalog is wired");
            match.Catalog = catalog;

            Assert.That(match.Catalog, Is.SameAs(catalog));
            Assert.That(match.Loadout, Is.Null, "the catalog is read when the match starts on the server");
        }

        [Test]
        public void LoadsEveryWeaponOfTheCatalog()
        {
            var loadout = RifleAndHandgunLoadout();

            Assert.That(loadout.Catalog.Count, Is.EqualTo(2));
            Assert.That(loadout.Catalog.Get(NetTeamDeathmatch.DefaultWeaponId).Damage, Is.EqualTo(25));
            Assert.That(loadout.Catalog.Get("handgun").Damage, Is.EqualTo(34));
            Assert.That(loadout.Count, Is.Zero, "nobody holds a weapon until they join");
        }

        [Test]
        public void RefusesToStartWithoutACatalog()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(null, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("no weapon catalog"));
        }

        [Test]
        public void RefusesToStartWithAnEmptyCatalog()
        {
            Assert.That(NetTeamDeathmatch.TryCreateLoadout(Catalog(), out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("TestWeaponCatalog"));
        }

        [Test]
        public void RefusesToStartWithABrokenWeapon()
        {
            var broken = Definition("Broken", NetTeamDeathmatch.DefaultWeaponId);
            broken.MagazineSize = 0;

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(Catalog(broken), out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain("Broken"));
        }

        [Test]
        public void RefusesToStartWithoutTheWeaponGivenToJoiningPlayers()
        {
            var catalog = Catalog(Definition("Handgun", "handgun"));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(catalog, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain(NetTeamDeathmatch.DefaultWeaponId));
        }

        [Test]
        public void RefusesToStartWithAnIdTooLongToSend()
        {
            var longId = new string('a', 30);
            var catalog = Catalog(Definition("Rifle", NetTeamDeathmatch.DefaultWeaponId), Definition("Long", longId));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(catalog, out var loadout, out var error), Is.False);
            Assert.That(loadout, Is.Null);
            Assert.That(error, Does.Contain(longId));
        }

        [Test]
        public void ShotWithAnUnknownWeaponIdIsDropped()
        {
            var loadout = RifleAndHandgunLoadout();
            loadout.Equip(0, NetTeamDeathmatch.DefaultWeaponId);

            Assert.That(loadout.TryFire(0, "shotgun", 0d, out var spec), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(spec, Is.Null);
            Assert.That(loadout.TryFire(0, string.Empty, 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
            Assert.That(loadout.TryFire(0, "RIFLE", 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
        }

        [Test]
        public void ShotWithAnotherWeaponThanTheEquippedOneIsDropped()
        {
            var loadout = RifleAndHandgunLoadout();
            loadout.Equip(0, NetTeamDeathmatch.DefaultWeaponId);

            // handgun は Catalog にあるが、サーバーが持たせているのは rifle
            Assert.That(loadout.TryFire(0, "handgun", 0d, out var spec), Is.EqualTo(ShotRejection.WeaponMismatch));
            Assert.That(spec, Is.Null);
        }

        [Test]
        public void ShotWithTheEquippedWeaponUsesTheServerValues()
        {
            var loadout = RifleAndHandgunLoadout();
            loadout.Equip(0, NetTeamDeathmatch.DefaultWeaponId);

            Assert.That(loadout.TryFire(0, NetTeamDeathmatch.DefaultWeaponId, 0d, out var spec), Is.EqualTo(ShotRejection.None));
            Assert.That(spec, Is.SameAs(loadout.Catalog.Get(NetTeamDeathmatch.DefaultWeaponId)));
            Assert.That(spec.Damage, Is.EqualTo(25));
        }

        [Test]
        public void DroppedShotsDoNotUseUpTheFireRate()
        {
            var loadout = RifleAndHandgunLoadout();
            loadout.Equip(0, NetTeamDeathmatch.DefaultWeaponId);

            for (var i = 0; i < 10; i++)
            {
                Assert.That(loadout.TryFire(0, "handgun", 0d, out _), Is.EqualTo(ShotRejection.WeaponMismatch));
                Assert.That(loadout.TryFire(0, "shotgun", 0d, out _), Is.EqualTo(ShotRejection.UnknownWeapon));
            }

            // 武器が正しい射撃だけが rifle の連射間隔 (ShotBudget) を減らす
            for (var i = 0; i < ShotBudget.DefaultBurst; i++)
            {
                Assert.That(loadout.TryFire(0, NetTeamDeathmatch.DefaultWeaponId, 0d, out _), Is.EqualTo(ShotRejection.None), "shot " + i);
            }

            Assert.That(loadout.TryFire(0, NetTeamDeathmatch.DefaultWeaponId, 0d, out _), Is.EqualTo(ShotRejection.FireRateExceeded));
        }

        [Test]
        public void FireRateFollowsTheEquippedWeapon()
        {
            var loadout = RifleAndHandgunLoadout();
            loadout.Equip(0, "handgun");
            for (var i = 0; i < ShotBudget.DefaultBurst; i++)
            {
                Assert.That(loadout.TryFire(0, "handgun", 0d, out _), Is.EqualTo(ShotRejection.None), "shot " + i);
            }

            // rifle の間隔 (0.1 秒) では handgun (0.2 秒) は 1 発ぶんも戻らない
            Assert.That(loadout.TryFire(0, "handgun", 0.1d, out _), Is.EqualTo(ShotRejection.FireRateExceeded));
            Assert.That(loadout.TryFire(0, "handgun", 0.3d, out _), Is.EqualTo(ShotRejection.None));
        }

        [Test]
        public void RejectedWeaponShotsUseTheSameWarning()
        {
            Assert.That(NetHitLog.Rejected(3, ShotRejection.UnknownWeapon), Is.EqualTo("[Net] rejected shot attacker=3 reason=UnknownWeapon"));
            Assert.That(NetHitLog.Rejected(3, ShotRejection.WeaponMismatch), Is.EqualTo("[Net] rejected shot attacker=3 reason=WeaponMismatch"));
        }
    }
}

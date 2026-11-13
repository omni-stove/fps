using Fps.Core;
using Fps.Editor;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Tests.EditMode
{
    /// <summary>
    /// EnemyAgent の武器なしの状態 (<see cref="EnemyAgent.Disarm"/>)、拾える物からの武器と予備弾の受け取り、モードが渡す行き先 (<see cref="EnemyAgent.SetErrand"/>) を確かめる。
    /// 行き先へ実際に歩く部分は NavMesh が要るので Play で確かめる。
    /// </summary>
    public sealed class EnemyAgentUnarmedTests
    {
        private GameObject _host;
        private EnemyAgent _agent;
        private WeaponDefinition _rifle;
        private WeaponDefinition _handgun;

        [SetUp]
        public void SetUp()
        {
            _rifle = NewWeapon("Rifle", WeaponAssetBuilder.RifleSpec());
            _handgun = NewWeapon("Handgun", WeaponAssetBuilder.HandgunSpec());

            _host = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            _host.name = "Bot";
            _agent = _host.AddComponent<EnemyAgent>();
            _agent.WeaponDefinition = _rifle;
            Assert.That(_agent.TryInitialize(), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
            Object.DestroyImmediate(_rifle);
            Object.DestroyImmediate(_handgun);
            foreach (var pickup in Object.FindObjectsByType<Pickup>(FindObjectsInactive.Include))
            {
                Object.DestroyImmediate(pickup.gameObject);
            }
        }

        [Test]
        public void ADisarmedBotHoldsNothingUntilItPicksUpAWeapon()
        {
            Assert.That(_agent.Disarm(), Is.True);
            Assert.That(_agent.Disarm(), Is.False, "already unarmed");

            Assert.That(_agent.IsUnarmed, Is.True);
            Assert.That(_agent.Weapon, Is.Null);
            Assert.That(_agent.DescribeWeapons(out var none), Is.Empty);
            Assert.That(none, Is.Null);
            Assert.That(_agent.AddReserveAmmo(_rifle.Id, 10), Is.Zero);

            var pickup = Pickup.Create(PickupContents.ForWeapon(_handgun, 5), _host.transform.position);
            Assert.That(pickup.TryCollect(_host.GetComponent<Damageable>(), out var outcome), Is.True);

            Assert.That(outcome.AddsWeapon, Is.True);
            Assert.That(_agent.IsUnarmed, Is.False);
            Assert.That(_agent.Weapon.Spec.Id, Is.EqualTo(_handgun.Id));
            Assert.That(_agent.WeaponDefinition, Is.SameAs(_handgun));
        }

        [Test]
        public void AnArmedBotTakesAmmoForItsOwnWeaponOnly()
        {
            var weapon = _agent.Weapon;
            var before = weapon.ReserveAmmo;
            weapon.TryFire();
            weapon.TryStartReload();
            weapon.Tick(weapon.Spec.ReloadSeconds);

            Assert.That(_agent.AddReserveAmmo(_handgun.Id, 10), Is.Zero);
            Assert.That(_agent.AddReserveAmmo(_rifle.Id, 10), Is.EqualTo(1), "only the one round used is missing");
            Assert.That(weapon.ReserveAmmo, Is.EqualTo(before));

            var states = _agent.DescribeWeapons(out var current);
            Assert.That(states.Count, Is.EqualTo(1));
            Assert.That(current, Is.EqualTo(_rifle.Id));
            Assert.That(_agent.AddWeapon(_rifle), Is.False, "already holds it");
        }

        [Test]
        public void AnErrandIsKeptUntilCleared()
        {
            Assert.That(_agent.Errand, Is.Null);

            _agent.SetErrand(new Vector3(1f, 0f, 2f), overridesCombat: true);

            Assert.That(_agent.Errand, Is.EqualTo((Vector3?)new Vector3(1f, 0f, 2f)));
            Assert.That(_agent.ErrandOverridesCombat, Is.True);

            _agent.ClearErrand();

            Assert.That(_agent.Errand, Is.Null);
            Assert.That(_agent.ErrandOverridesCombat, Is.False);
        }

        private static WeaponDefinition NewWeapon(string name, WeaponSpec spec)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            definition.name = name;
            WeaponAssetBuilder.Configure(definition, spec);
            return definition;
        }
    }
}

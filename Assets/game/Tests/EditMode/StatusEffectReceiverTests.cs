using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class StatusEffectReceiverTests
    {
        private const string _weaponId = "78afe32d-bf3f-459c-98b8-8c238290913f";
        private const string _flamesName = StatusEffectVisuals.ObjectNamePrefix + StatusEffectKinds.Burn;

        private readonly List<Object> _created = new List<Object>();
        private readonly List<KillFeedEntry> _kills = new List<KillFeedEntry>();

        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearSubscribers();
            CombatEvents.Killed += _kills.Add;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearSubscribers();
            _kills.Clear();
            foreach (var created in _created)
            {
                if (created != null)
                {
                    Object.DestroyImmediate(created);
                }
            }

            _created.Clear();
        }

        [Test]
        public void TheBurnDamagesEveryTickShowsFlamesAndGoesOut()
        {
            var target = CreateTarget(100);
            var receiver = target.EnsureStatusEffects();

            receiver.Afflict(new StatusEffectSpec(2f, 0.5f, 5), Create("shooter"), _weaponId);

            Assert.That(receiver.Has(StatusEffectKinds.Burn), Is.True);
            Assert.That(target.transform.Find(_flamesName), Is.Not.Null, "with no visual in the catalog the flames are built in code");

            receiver.Advance(1f);
            Assert.That(target.Health.Current, Is.EqualTo(90));

            receiver.Advance(1.5f);
            Assert.That(target.Health.Current, Is.EqualTo(80), "no damage after the time runs out");
            Assert.That(receiver.Has(StatusEffectKinds.Burn), Is.False);
            Assert.That(target.transform.Find(_flamesName), Is.Null);
            Assert.That(_kills, Is.Empty);
        }

        [Test]
        public void AKillByBurningIsCreditedToTheShooterAndTheWeapon()
        {
            var target = CreateTarget(20);
            var shooter = Create("shooter");
            var receiver = target.EnsureStatusEffects();

            receiver.Afflict(new StatusEffectSpec(5f, 0.5f, 15), shooter, _weaponId);
            receiver.Advance(1f);

            Assert.That(target.IsDead, Is.True);
            Assert.That(_kills, Has.Count.EqualTo(1));
            Assert.That(_kills[0].KillerId, Is.EqualTo(CombatEvents.OfflineId(shooter)));
            Assert.That(_kills[0].VictimId, Is.EqualTo(CombatEvents.OfflineId(target.gameObject)));
            Assert.That(_kills[0].WeaponId, Is.EqualTo(_weaponId));
            Assert.That(receiver.Active, Is.Empty, "dying puts the fire out");
            Assert.That(target.transform.Find(_flamesName), Is.Null);
        }

        [Test]
        public void ADeadTargetDoesNotCatchFire()
        {
            var target = CreateTarget(10);
            target.Kill();

            target.EnsureStatusEffects().Afflict(new StatusEffectSpec(2f, 0.5f, 5), null, _weaponId);

            Assert.That(target.StatusEffects.Has(StatusEffectKinds.Burn), Is.False);
        }

        [Test]
        public void ResettingHealthPutsTheFireOut()
        {
            var target = CreateTarget(100);
            var receiver = target.EnsureStatusEffects();
            receiver.Afflict(new StatusEffectSpec(5f, 0.5f, 5), null, _weaponId);

            target.ResetHealth();

            Assert.That(receiver.Active, Is.Empty);
            receiver.Advance(1f);
            Assert.That(target.Health.Current, Is.EqualTo(100));
        }

        private Damageable CreateTarget(int maxHealth)
        {
            var target = Create("target").AddComponent<Damageable>();
            target.Initialize("target", maxHealth);
            return target;
        }

        private GameObject Create(string name)
        {
            var created = new GameObject(name);
            _created.Add(created);
            return created;
        }
    }
}

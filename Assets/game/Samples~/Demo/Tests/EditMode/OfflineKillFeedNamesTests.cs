using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shooter.Tests.EditMode
{
    public sealed class OfflineKillFeedNamesTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GameObject _killer;
        private GameObject _victim;
        private AbilityHost _host;

        [SetUp]
        public void SetUp()
        {
            _killer = Track(new GameObject("Killer"));
            _killer.AddComponent<Damageable>().Initialize("Killer", 100);
            _host = _killer.AddComponent<AbilityHost>();
            _victim = Track(new GameObject("Victim"));
            _victim.AddComponent<Damageable>().Initialize("Victim", 100);
        }

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

        [Test]
        public void AMineKillIsNamedAfterTheEquippedMine()
        {
            var mine = Track(ScriptableObject.CreateInstance<MineDeployDefinition>());
            mine.Id = "mine";
            _host.Equip(0, mine);

            var line = new OfflineKillFeedNames().Describe(Entry("mine"));

            Assert.That(line.WeaponName, Is.EqualTo(new LocalizedText(MineSpec.DefaultNameKey)));
        }

        [Test]
        public void ARemoteChargeKillIsNamedAfterTheEquippedCharge()
        {
            var charge = Track(ScriptableObject.CreateInstance<RemoteChargeDeployDefinition>());
            charge.Id = "remote-charge";
            _host.Equip(0, charge);

            var line = new OfflineKillFeedNames().Describe(Entry("remote-charge"));

            Assert.That(line.WeaponName, Is.EqualTo(new LocalizedText(RemoteChargeSpec.DefaultNameKey)));
        }

        [Test]
        public void AnIdTheKillerDoesNotCarryHasNoName()
        {
            var line = new OfflineKillFeedNames().Describe(Entry("mine"));

            Assert.That(line.WeaponName, Is.EqualTo(default(LocalizedText)));
        }

        private KillFeedEntry Entry(string weaponId)
        {
            return new KillFeedEntry(CombatEvents.OfflineId(_killer), CombatEvents.OfflineId(_victim), weaponId, false, false);
        }

        private T Track<T>(T created)
            where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}

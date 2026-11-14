using System.Collections.Generic;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Fps.Net.Tests
{
    /// <summary>
    /// 試合に入ったときに、保存したロードアウトを送るかどうか (<see cref="NetLoadoutChooser.SavedLoadoutToSend"/>)。
    /// 送った並びはサーバーが同じアセット (<see cref="NetTeamDeathmatch.TryCreateLoadout(LoadoutDefinition, WeaponCatalogAsset, out ServerLoadout, out string)"/>)
    /// で受け入れること。
    /// </summary>
    public sealed class NetLoadoutChooserTests
    {
        private const string _rifleId = "00000000-0000-4000-8000-00000000000a";
        private const string _handgunId = "00000000-0000-4000-8000-000000000002";
        private const string _smgId = "00000000-0000-4000-8000-000000000004";
        private const string _removedId = "00000000-0000-4000-8000-000000000009";

        private readonly List<Object> _created = new List<Object>();
        private LoadoutDefinition _loadout;
        private WeaponCatalogAsset _catalog;

        [SetUp]
        public void SetUp()
        {
            var rifle = Definition("Rifle", _rifleId, WeaponCategory.AssaultRifle);
            var handgun = Definition("Handgun", _handgunId, WeaponCategory.Handgun);
            var smg = Definition("Smg", _smgId, WeaponCategory.SubmachineGun);

            _loadout = ScriptableObject.CreateInstance<LoadoutDefinition>();
            _created.Add(_loadout);
            _loadout.name = "TestLoadout";
            _loadout.SetSlots(new[]
            {
                new LoadoutSlotDefinition(new[] { WeaponCategory.AssaultRifle, WeaponCategory.SubmachineGun }, rifle),
                new LoadoutSlotDefinition(new[] { WeaponCategory.Handgun }, handgun),
            });

            _catalog = ScriptableObject.CreateInstance<WeaponCatalogAsset>();
            _created.Add(_catalog);
            _catalog.name = "TestCatalog";
            _catalog.SetWeapons(new[] { rifle, handgun, smg });
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

        private WeaponDefinition Definition(string assetName, string id, WeaponCategory category)
        {
            var definition = ScriptableObject.CreateInstance<WeaponDefinition>();
            _created.Add(definition);
            definition.name = assetName;
            definition.Id = id;
            definition.NameKey = assetName;
            definition.Category = category;
            return definition;
        }

        [Test]
        public void SendsASavedLoadoutTheServerAccepts()
        {
            var ids = NetLoadoutChooser.SavedLoadoutToSend(new[] { _smgId, _handgunId }, _loadout, _catalog, out var reason);

            Assert.That(reason, Is.Null);
            Assert.That(ids, Is.EqualTo(new[] { _smgId, _handgunId }));

            Assert.That(NetTeamDeathmatch.TryCreateLoadout(_loadout, _catalog, out var server, out var error), Is.True, error);
            Assert.That(server.TryChoose(1, ids, out reason), Is.True, reason);
        }

        [Test]
        public void SendsNothingWhenNothingIsSaved()
        {
            Assert.That(NetLoadoutChooser.SavedLoadoutToSend(null, _loadout, _catalog, out var reason), Is.Null);
            Assert.That(reason, Is.Null);
        }

        [Test]
        public void DoesNotSendASavedLoadoutThisMatchNoLongerOffers()
        {
            Assert.That(NetLoadoutChooser.SavedLoadoutToSend(new[] { _removedId, _handgunId }, _loadout, _catalog, out var reason), Is.Null);
            Assert.That(reason, Does.Contain("does not offer"));

            Assert.That(NetLoadoutChooser.SavedLoadoutToSend(new[] { _handgunId, _smgId }, _loadout, _catalog, out reason), Is.Null,
                "weapons saved in slots that no longer accept them");
            Assert.That(reason, Is.Not.Null);

            Assert.That(NetLoadoutChooser.SavedLoadoutToSend(new[] { _smgId }, _loadout, _catalog, out reason), Is.Null,
                "a loadout saved for a different number of slots");
            Assert.That(reason, Is.Not.Null);
        }

        [Test]
        public void DoesNotSendWithoutTheMatchsCatalog()
        {
            Assert.That(NetLoadoutChooser.SavedLoadoutToSend(new[] { _smgId, _handgunId }, _loadout, null, out var reason), Is.Null);
            Assert.That(reason, Does.Contain("catalog"));
        }
    }
}

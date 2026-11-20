using System;
using System.Text.RegularExpressions;
using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class LoadoutPreferenceStoreTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";
        private const string _beacon = "beacon";

        private LoadoutPreferenceStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の選択 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new LoadoutPreferenceStore("Shooter.Tests.LoadoutChoice." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void UsesItsOwnKey()
        {
            Assert.That(LoadoutPreferenceStore.DefaultKey, Is.EqualTo("Shooter.LoadoutChoice"));
            Assert.That(new LoadoutPreferenceStore().Key, Is.EqualTo(LoadoutPreferenceStore.DefaultKey));
            Assert.Throws<ArgumentNullException>(() => new LoadoutPreferenceStore(null));
            Assert.Throws<ArgumentException>(() => new LoadoutPreferenceStore(string.Empty));
        }

        [Test]
        public void LoadsNothingSilentlyBeforeTheFirstSave()
        {
            Assert.That(_store.Load(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesToPlayerPrefsAndLoadsItBackFromANewStore()
        {
            _store.Save(new LoadoutChoice(new[] { _rifle, _handgun }));

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(
                "{\"version\":2,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}"));
            var loaded = new LoadoutPreferenceStore(_store.Key).Load();
            Assert.That(loaded.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(loaded.AbilityIds, Is.Null, "a choice saved without abilities keeps the loadout's abilities");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesTheAbilitiesWithTheWeaponsAndKeepsANoneSlot()
        {
            _store.Save(new LoadoutChoice(new[] { _rifle, _handgun }, new[] { _beacon, AbilitySelection.None }));

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(
                "{\"version\":2,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"],\"abilities\":[\"beacon\",\"\"]}"));
            var loaded = new LoadoutPreferenceStore(_store.Key).Load();
            Assert.That(loaded.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(loaded.AbilityIds, Is.EqualTo(new[] { _beacon, string.Empty }));
        }

        [Test]
        public void LoadsTheWeaponsOfAChoiceSavedBeforeAbilities()
        {
            PlayerPrefs.SetString(_store.Key, "{\"version\":1,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}");

            var loaded = _store.Load();

            Assert.That(loaded.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(loaded.AbilityIds, Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SaveReplacesTheEarlierChoice()
        {
            _store.Save(new LoadoutChoice(new[] { _rifle, _handgun }, new[] { _beacon }));

            _store.Save(new LoadoutChoice(new[] { _handgun, _rifle }));

            var loaded = _store.Load();
            Assert.That(loaded.WeaponIds, Is.EqualTo(new[] { _handgun, _rifle }));
            Assert.That(loaded.AbilityIds, Is.Null, "the earlier abilities are not carried over");
        }

        [Test]
        public void SaveRejectsAnEmptyChoiceAndKeepsTheEarlierOne()
        {
            _store.Save(new LoadoutChoice(new[] { _rifle, _handgun }));

            Assert.Throws<ArgumentException>(() => _store.Save(new LoadoutChoice(new[] { _rifle, string.Empty })));
            Assert.Throws<ArgumentException>(() => _store.Save(new LoadoutChoice(Array.Empty<string>())));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));

            Assert.That(_store.Load().WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
        }

        [Test]
        public void DeleteRemovesTheKey()
        {
            _store.Save(new LoadoutChoice(new[] { _rifle, _handgun }));

            _store.Delete();

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(_store.Load(), Is.Null);
        }

        [Test]
        public void BrokenTextLoadsNothingWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Loadout\] .*not valid JSON"));

            Assert.That(_store.Load(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AbilitiesThatAreNotAListLoadNothingWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, "{\"version\":2,\"weapons\":[\"" + _rifle + "\"],\"abilities\":\"beacon\"}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Loadout\] .*abilities are not a list"));

            Assert.That(_store.Load(), Is.Null);
            LogAssert.NoUnexpectedReceived();
        }
    }
}

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
            _store = new LoadoutPreferenceStore("Shooter.Tests.LoadoutChoice." + Guid.NewGuid().ToString("N"), new LoadoutPresetSpec(3, 8));
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
                "{\"version\":3,\"active\":0,\"presets\":[{\"name\":\"\",\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"]}]}"));
            var loaded = new LoadoutPreferenceStore(_store.Key, _store.Spec).Load();
            Assert.That(loaded.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(loaded.AbilityIds, Is.Null, "a choice saved without abilities keeps the loadout's abilities");
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesTheAbilitiesWithTheWeaponsAndKeepsANoneSlot()
        {
            _store.Save(new LoadoutChoice(new[] { _rifle, _handgun }, new[] { _beacon, AbilitySelection.None }));

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(
                "{\"version\":3,\"active\":0,\"presets\":[{\"name\":\"\",\"weapons\":[\"" + _rifle + "\",\"" + _handgun
                + "\"],\"abilities\":[\"beacon\",\"\"]}]}"));
            var loaded = new LoadoutPreferenceStore(_store.Key, _store.Spec).Load();
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

        [Test]
        public void WithoutASpecTheLimitComesFromKitTuning()
        {
            KitTuning.Reload();

            Assert.That(new LoadoutPreferenceStore(_store.Key).Spec.MaxPresets, Is.EqualTo(KitTuning.LoadoutPresets.MaxPresets));
            Assert.That(_store.Spec.MaxPresets, Is.EqualTo(3));
        }

        [Test]
        public void LoadPresetsIsEmptyBeforeTheFirstSave()
        {
            var book = _store.LoadPresets();

            Assert.That(book.Count, Is.EqualTo(0));
            Assert.That(book.MaxPresets, Is.EqualTo(3));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void TheOldSingleChoiceBecomesTheFirstPresetAndIsKeptWhenPresetsAreAdded()
        {
            PlayerPrefs.SetString(_store.Key, "{\"version\":2,\"weapons\":[\"" + _rifle + "\",\"" + _handgun + "\"],\"abilities\":[\"beacon\"]}");

            var book = _store.LoadPresets();
            Assert.That(book.Count, Is.EqualTo(1));
            Assert.That(book.ActiveIndex, Is.EqualTo(0));
            Assert.That(book.Active.HasName, Is.False);

            book.Add("Close", new LoadoutChoice(new[] { _handgun, _rifle }));
            _store.SavePresets(book);

            var reloaded = new LoadoutPreferenceStore(_store.Key, _store.Spec).LoadPresets();
            Assert.That(reloaded.Count, Is.EqualTo(2));
            Assert.That(reloaded[0].Choice.WeaponIds, Is.EqualTo(new[] { _rifle, _handgun }));
            Assert.That(reloaded[0].Choice.AbilityIds, Is.EqualTo(new[] { _beacon }));
            Assert.That(reloaded[1].Name, Is.EqualTo("Close"));
            Assert.That(PlayerPrefs.GetString(_store.Key), Does.StartWith("{\"version\":3,"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LoadAndSaveUseTheActivePreset()
        {
            var book = _store.LoadPresets();
            book.Add("A", new LoadoutChoice(new[] { _rifle }));
            book.Add("B", new LoadoutChoice(new[] { _handgun }));
            book.Select(1);
            _store.SavePresets(book);

            Assert.That(_store.Load().WeaponIds, Is.EqualTo(new[] { _handgun }));

            _store.Save(new LoadoutChoice(new[] { _handgun, _rifle }, new[] { _beacon }));

            var reloaded = _store.LoadPresets();
            Assert.That(reloaded.ActiveIndex, Is.EqualTo(1));
            Assert.That(reloaded[1].Name, Is.EqualTo("B"), "saving keeps the preset's name");
            Assert.That(reloaded[1].Choice.WeaponIds, Is.EqualTo(new[] { _handgun, _rifle }));
            Assert.That(reloaded[0].Choice.WeaponIds, Is.EqualTo(new[] { _rifle }), "the other presets are untouched");
        }

        [Test]
        public void PresetsBeyondALoweredLimitAreDroppedWithAWarning()
        {
            var wide = new LoadoutPreferenceStore(_store.Key, new LoadoutPresetSpec(5));
            var book = wide.LoadPresets();
            for (var i = 0; i < 5; i++)
            {
                book.Add("P" + i, new LoadoutChoice(new[] { _rifle }));
            }

            wide.SavePresets(book);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Loadout\] .*2 presets beyond the limit of 3 are not kept$"));

            var narrowed = _store.LoadPresets();

            Assert.That(narrowed.Count, Is.EqualTo(3));
            Assert.That(narrowed[2].Name, Is.EqualTo("P2"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenSaveLoadsAnEmptyBookWithAWarning()
        {
            PlayerPrefs.SetString(_store.Key, "{\"version\":3,\"presets\":[1]}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Loadout\] .*preset 0: it is not a JSON object"));

            Assert.That(_store.LoadPresets().Count, Is.EqualTo(0));
            LogAssert.NoUnexpectedReceived();
        }
    }
}

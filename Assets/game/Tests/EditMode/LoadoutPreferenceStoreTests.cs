using System;
using System.Text.RegularExpressions;
using Fps.Core;
using Fps.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Fps.Tests.EditMode
{
    public sealed class LoadoutPreferenceStoreTests
    {
        private const string _rifle = "00000000-0000-4000-8000-000000000001";
        private const string _handgun = "00000000-0000-4000-8000-000000000002";

        private LoadoutPreferenceStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の選択 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new LoadoutPreferenceStore("Fps.Tests.LoadoutChoice." + Guid.NewGuid().ToString("N"));
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
            Assert.That(LoadoutPreferenceStore.DefaultKey, Is.EqualTo("Fps.LoadoutChoice"));
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
            _store.Save(new[] { _rifle, _handgun });

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(LoadoutPreference.ToJson(new[] { _rifle, _handgun })));
            Assert.That(new LoadoutPreferenceStore(_store.Key).Load(), Is.EqualTo(new[] { _rifle, _handgun }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SaveReplacesTheEarlierChoice()
        {
            _store.Save(new[] { _rifle, _handgun });

            _store.Save(new[] { _handgun, _rifle });

            Assert.That(_store.Load(), Is.EqualTo(new[] { _handgun, _rifle }));
        }

        [Test]
        public void SaveRejectsAnEmptyChoiceAndKeepsTheEarlierOne()
        {
            _store.Save(new[] { _rifle, _handgun });

            Assert.Throws<ArgumentException>(() => _store.Save(new[] { _rifle, string.Empty }));

            Assert.That(_store.Load(), Is.EqualTo(new[] { _rifle, _handgun }));
        }

        [Test]
        public void DeleteRemovesTheKey()
        {
            _store.Save(new[] { _rifle, _handgun });

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
    }
}

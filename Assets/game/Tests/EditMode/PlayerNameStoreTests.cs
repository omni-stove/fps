using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class PlayerNameStoreTests
    {
        // 生の制御文字をファイルに置かないよう、数値の (char) で組み立てる
        private static readonly string[] _refusedNames =
        {
            "Ace\nBob",
            "Ace\tBob",
            "Ace" + (char)0x2028 + "Bob",
            "Ace" + (char)0x202E + "Bob",
            "Ace" + (char)0xD800,
            "ABCDEFGHIJKLMNOPQ",
        };

        private PlayerNameStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の名前 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new PlayerNameStore("Shooter.Tests.PlayerName." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(_store.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void TheGameKeyIsItsOwn()
        {
            Assert.That(PlayerNameStore.DefaultKey, Is.EqualTo("Shooter.PlayerName"));
            Assert.That(PlayerNameStore.DefaultKey, Is.Not.EqualTo(VoiceSettingsStore.DefaultKey));
            Assert.That(new PlayerNameStore().MaxLength, Is.EqualTo(NetMatchSettings.DefaultMaxDisplayNameLength));
            Assert.That(new PlayerNameStore().MaxLength, Is.EqualTo(16));
        }

        [Test]
        public void LoadsEmptySilentlyBeforeTheFirstSave()
        {
            Assert.That(_store.Load(), Is.EqualTo(string.Empty));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASavedNameLoadsBackTrimmed()
        {
            Assert.That(_store.TrySave("  Ace 7  "), Is.True);

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo("Ace 7"));
            Assert.That(new PlayerNameStore(_store.Key).Load(), Is.EqualTo("Ace 7"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AJapaneseNameUpToTheLimitIsKept()
        {
            Assert.That(_store.TrySave("あいうえおかきくけこさしすせそた"), Is.True);

            Assert.That(_store.Load(), Is.EqualTo("あいうえおかきくけこさしすせそた"));
        }

        [TestCaseSource(nameof(_refusedNames))]
        public void ARefusedNameIsNotSavedAndKeepsTheOldOne(string name)
        {
            _store.TrySave("Ace");

            Assert.That(_store.TrySave(name), Is.False);

            Assert.That(_store.Load(), Is.EqualTo("Ace"));
        }

        [Test]
        public void ARefusedNameBeforeAnySaveLeavesNothingSaved()
        {
            Assert.That(_store.TrySave("Ace\nBob"), Is.False);

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(_store.Load(), Is.EqualTo(string.Empty));
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase(null)]
        public void AnEmptyNameForgetsTheSavedOne(string name)
        {
            _store.TrySave("Ace");

            Assert.That(_store.TrySave(name), Is.True);

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(_store.Load(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ASmallerLimitRefusesALongerName()
        {
            var store = new PlayerNameStore(_store.Key, 4);

            Assert.That(store.TrySave("Alice"), Is.False);
            Assert.That(store.TrySave("Bob"), Is.True);
            Assert.That(store.Load(), Is.EqualTo("Bob"));
        }

        [Test]
        public void SavingTellsTheKeyAndTheName()
        {
            var saved = new List<KeyValuePair<string, string>>();
            void Record(string key, string name) => saved.Add(new KeyValuePair<string, string>(key, name));
            PlayerNameStore.Saved += Record;
            try
            {
                _store.TrySave(" Ace ");
                _store.TrySave("Ace\nBob");
                _store.TrySave("");
            }
            finally
            {
                PlayerNameStore.Saved -= Record;
            }

            Assert.That(saved, Is.EqualTo(new[]
            {
                new KeyValuePair<string, string>(_store.Key, "Ace"),
                new KeyValuePair<string, string>(_store.Key, string.Empty),
            }));
        }

        [Test]
        public void ABrokenSavedNameWarnsAndLoadsEmpty()
        {
            PlayerPrefs.SetString(_store.Key, "Ace\nBob");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Name\] saved name '.*' is not a name of 1 to 16 characters"));

            Assert.That(_store.Load(), Is.EqualTo(string.Empty));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ANameLongerThanTheLimitUnderTheKeyWarnsAndLoadsEmpty()
        {
            PlayerPrefs.SetString(_store.Key, "Alice");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Name\] saved name '.*' is not a name of 1 to 4 characters"));

            Assert.That(new PlayerNameStore(_store.Key, 4).Load(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void ANumberUnderTheKeyWarnsAndLoadsEmpty()
        {
            PlayerPrefs.SetInt(_store.Key, 3);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Name\] saved name"));

            Assert.That(_store.Load(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void DeleteForgetsTheSavedName()
        {
            _store.TrySave("Ace");

            _store.Delete();

            Assert.That(_store.Load(), Is.EqualTo(string.Empty));
        }

        [Test]
        public void RejectsABadKeyOrLimit()
        {
            Assert.Throws<ArgumentException>(() => new PlayerNameStore(""));
            Assert.Throws<ArgumentNullException>(() => new PlayerNameStore(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerNameStore(_store.Key, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new PlayerNameStore(_store.Key, NetMatchSettings.MaxMaxDisplayNameLength + 1));
            Assert.DoesNotThrow(() => new PlayerNameStore(_store.Key, NetMatchSettings.MaxMaxDisplayNameLength));
        }
    }
}

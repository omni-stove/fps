using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class BlockListStoreTests
    {
        private const string _alice = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _bob = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private BlockListStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の一覧 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new BlockListStore("Shooter.Tests.BlockList." + Guid.NewGuid().ToString("N"));
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
            Assert.That(BlockListStore.DefaultKey, Is.EqualTo("Shooter.BlockList"));
            Assert.That(BlockListStore.DefaultKey, Is.Not.EqualTo(VoiceSettingsStore.DefaultKey));
            Assert.That(BlockListStore.DefaultKey, Is.Not.EqualTo(ControlSettingsStore.DefaultKey));
        }

        [Test]
        public void LoadsAnEmptyListSilentlyBeforeTheFirstSave()
        {
            var list = _store.Load();

            Assert.That(list.BlockedCount, Is.Zero);
            Assert.That(list.MutedCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASavedListLoadsBackInANewStore()
        {
            var list = new BlockList();
            list.SetBlocked(_alice, true);
            list.SetMuted(_bob, true);

            _store.Save(list);

            Assert.That(PlayerPrefs.GetString(_store.Key), Is.EqualTo(list.ToJson()));
            var read = new BlockListStore(_store.Key).Load();
            Assert.That(read.Blocked, Is.EqualTo(new[] { _alice }));
            Assert.That(read.Muted, Is.EqualTo(new[] { _bob }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABrokenValueWarnsAndLoadsAnEmptyList()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Block\] block list '.*': not valid JSON"));

            Assert.That(_store.Load().BlockedCount, Is.Zero);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ABadEntryWarnsAndTheRestIsKept()
        {
            PlayerPrefs.SetString(_store.Key, "{\"version\":1,\"blocked\":[\"" + _alice + "\",7],\"muted\":[]}");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Block\] block list '.*': 'blocked'\[1\] is not a user id; skipped"));

            var list = _store.Load();

            Assert.That(list.Blocked, Is.EqualTo(new[] { _alice }));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ANumberUnderTheKeyWarnsAndLoadsAnEmptyList()
        {
            PlayerPrefs.SetInt(_store.Key, 3);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Block\] block list '.*': not valid JSON"));

            Assert.That(_store.Load().BlockedCount, Is.Zero);
        }

        [Test]
        public void DeleteForgetsTheSavedList()
        {
            var list = new BlockList();
            list.SetBlocked(_alice, true);
            _store.Save(list);

            _store.Delete();

            Assert.That(_store.Load().BlockedCount, Is.Zero);
        }

        [Test]
        public void BadArgumentsAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new BlockListStore(""));
            Assert.Throws<ArgumentNullException>(() => new BlockListStore(null));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));
        }
    }
}

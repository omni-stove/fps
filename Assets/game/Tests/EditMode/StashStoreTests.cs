using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Shooter.Tests.EditMode
{
    public sealed class StashStoreTests
    {
        private const string _ammo = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e001";
        private const string _bandage = "5b0c55a4-8f0e-4a43-9c55-0b1f3ad1e002";

        private static readonly string[] _north = { "north" };

        private StashStore _store;

        [SetUp]
        public void SetUp()
        {
            // 本物の持ち物庫 (DefaultKey) を壊さないよう、テストごとに別のキーを使う。
            _store = new StashStore("Shooter.Tests.Stash." + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            _store.Delete();
        }

        private static StashSpec Spec(int capacity = 10)
        {
            return new StashSpec(capacity, new[] { new StashEntry(_ammo, 30) });
        }

        private static ItemCatalog Catalog()
        {
            return new ItemCatalog(new[] { new ItemSpec(_ammo, maxStack: 30), new ItemSpec(_bandage, maxStack: 5) }, InventoryRule.Default);
        }

        private static void AssertStacks(Stash stash, params (string ItemId, int Count)[] expected)
        {
            Assert.That(stash.Stacks.Count, Is.EqualTo(expected.Length));
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.That(stash.Stacks[i].ItemId, Is.EqualTo(expected[i].ItemId), "stack " + i);
                Assert.That(stash.Stacks[i].Count, Is.EqualTo(expected[i].Count), "stack " + i);
            }
        }

        [Test]
        public void UsesItsOwnKey()
        {
            Assert.That(StashStore.DefaultKey, Is.EqualTo("Shooter.Stash"));
            Assert.That(new StashStore().Key, Is.EqualTo(StashStore.DefaultKey));
            Assert.That(_store.BrokenKey, Is.EqualTo(_store.Key + ".broken"));
            Assert.Throws<ArgumentNullException>(() => new StashStore(null));
            Assert.Throws<ArgumentException>(() => new StashStore(string.Empty));
        }

        [Test]
        public void StartsFromTheStartingItemsSilentlyBeforeTheFirstSave()
        {
            var stash = _store.Load(Spec(), Catalog());

            Assert.That(_store.HasSave, Is.False);
            Assert.That(stash.Capacity, Is.EqualTo(10));
            AssertStacks(stash, (_ammo, 30));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SavesToPlayerPrefsAndLoadsItBackFromANewStore()
        {
            var stash = _store.Load(Spec(), Catalog());
            stash.Add(_bandage, 7);

            _store.Save(stash);

            Assert.That(
                PlayerPrefs.GetString(_store.Key),
                Is.EqualTo("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _ammo + "\",\"count\":30},{\"itemId\":\"" + _bandage
                    + "\",\"count\":5},{\"itemId\":\"" + _bandage + "\",\"count\":2}]}"));
            var loaded = new StashStore(_store.Key).Load(Spec(), Catalog());
            AssertStacks(loaded, (_ammo, 30), (_bandage, 5), (_bandage, 2));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ItemsPickedUpInARaidAreThereAfterExtractingAndReloading()
        {
            var stash = _store.Load(Spec(), Catalog());
            var raid = new ExtractionSession(
                new ExtractionSpec(raidSeconds: 60f, extractHoldSeconds: 2f, openPoints: 1, openAfterSeconds: 0f),
                stash,
                _north,
                new[] { new ItemCount(_ammo, 10) });
            raid.Pick(_bandage, 3);
            raid.Tick(2f, "north");
            _store.Save(stash);

            var loaded = new StashStore(_store.Key).Load(Spec(), Catalog());

            Assert.That(raid.Outcome, Is.EqualTo(ExtractionOutcome.Extracted));
            Assert.That(loaded.CountOf(_ammo), Is.EqualTo(30));
            Assert.That(loaded.CountOf(_bandage), Is.EqualTo(3));
        }

        [Test]
        public void ItemsBroughtIntoARaidAreGoneAfterDyingAndReloading()
        {
            var stash = _store.Load(Spec(), Catalog());
            var raid = new ExtractionSession(ExtractionSpec.Default, stash, _north, new[] { new ItemCount(_ammo, 10) });
            raid.Pick(_bandage, 3);
            raid.Die();
            _store.Save(stash);

            var loaded = new StashStore(_store.Key).Load(Spec(), Catalog());

            Assert.That(raid.Outcome, Is.EqualTo(ExtractionOutcome.Died));
            Assert.That(loaded.CountOf(_ammo), Is.EqualTo(20));
            Assert.That(loaded.CountOf(_bandage), Is.EqualTo(0));
        }

        [Test]
        public void BrokenTextStartsOverWithAWarningAndKeepsTheText()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Stash\] .*not valid JSON.*\.broken"));

            var stash = _store.Load(Spec(), Catalog());

            AssertStacks(stash, (_ammo, 30));
            Assert.That(PlayerPrefs.GetString(_store.BrokenKey), Is.EqualTo("{not json"));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ASaveLargerThanTheCapacityKeepsEveryStackWithAWarning()
        {
            var stash = _store.Load(Spec(), Catalog());
            stash.Add(_bandage, 10);
            _store.Save(stash);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Stash\] .*has 3 stacks, more than the capacity 1"));

            var loaded = _store.Load(Spec(1), Catalog());

            AssertStacks(loaded, (_ammo, 30), (_bandage, 5), (_bandage, 5));
            Assert.That(loaded.Status.Capacity, Is.EqualTo(3));
        }

        [Test]
        public void DeleteRemovesTheSaveAndTheBrokenCopy()
        {
            PlayerPrefs.SetString(_store.Key, "{not json");
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[Stash\] "));
            _store.Load(Spec(), Catalog());

            _store.Delete();

            Assert.That(PlayerPrefs.HasKey(_store.Key), Is.False);
            Assert.That(PlayerPrefs.HasKey(_store.BrokenKey), Is.False);
        }

        [Test]
        public void RejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => _store.Load(null, Catalog()));
            Assert.Throws<ArgumentNullException>(() => _store.Load(Spec(), null));
            Assert.Throws<ArgumentNullException>(() => _store.Save(null));
        }
    }
}

using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Server.Tests
{
    // アカウントごとの持ち物庫のファイルを、書いて読み戻す・壊れたファイルを退ける・置き場所を確かめる
    public sealed class ServerStashStoreTests
    {
        private const string _herbId = "0d3c6f1a-5b2e-4c7d-8a9f-1e2b3c4d5e6f";
        private const string _rifleItemId = "4d5e6f7a-8b9c-4d0e-9f2a-3b4c5d6e7f8a";
        private const string _account = "0123456789abcdef0123456789abcdef";

        private TempDirectory _root;
        private StringWriter _log;
        private ServerStashStore _store;

        [SetUp]
        public void SetUp()
        {
            _root = new TempDirectory();
            _log = new StringWriter();
            Assert.That(ServerStashStore.TryOpen(Path.Combine(_root.Path, "stashes"), _log, out _store, out var error), Is.True, error);
        }

        [TearDown]
        public void TearDown()
        {
            _log.Dispose();
            _root.Dispose();
        }

        [Test]
        public void OpeningCreatesTheDirectoryAndLeavesNothingInIt()
        {
            Assert.That(Directory.Exists(_store.Directory), Is.True);
            Assert.That(_store.Directory, Is.EqualTo(Path.GetFullPath(Path.Combine(_root.Path, "stashes"))));
            Assert.That(Directory.GetFileSystemEntries(_store.Directory), Is.Empty, "the write check leaves no file behind");
        }

        [Test]
        public void ASavedStashReadsBackTheSameStacks()
        {
            var stash = Stash.Fresh(Spec(), Items());
            stash.Add(_herbId, 7);
            stash.Add(_rifleItemId, 1);

            _store.Save(_account, stash);
            var loaded = _store.Load(_account, Spec(), Items());

            Assert.That(_store.HasSave(_account), Is.True);
            Assert.That(loaded.Stacks.Select(s => (s.ItemId, s.Count)), Is.EqualTo(new[] { (_herbId, 5), (_herbId, 2), (_rifleItemId, 1) }));
            Assert.That(loaded.Capacity, Is.EqualTo(Spec().Capacity));
            Assert.That(File.ReadAllText(_store.PathOf(_account)), Is.EqualTo(StashSave.ToJson(stash.Stacks)));
            Assert.That(File.Exists(_store.PathOf(_account) + ServerStashStore.TemporarySuffix), Is.False, "the temporary file is moved into place");
        }

        [Test]
        public void SavingAgainReplacesTheFile()
        {
            var stash = Stash.Fresh(Spec(), Items());
            stash.Add(_herbId, 3);
            _store.Save(_account, stash);
            stash.Remove(_herbId, 3);

            _store.Save(_account, stash);

            Assert.That(_store.Load(_account, Spec(), Items()).Stacks, Is.Empty);
        }

        [Test]
        public void AnAccountWithoutAFileStartsFromTheStartingItems()
        {
            var loaded = _store.Load(_account, Spec(new StashEntry(_rifleItemId, 1)), Items());

            Assert.That(_store.HasSave(_account), Is.False);
            Assert.That(loaded.CountOf(_rifleItemId), Is.EqualTo(1));
            Assert.That(File.Exists(_store.PathOf(_account)), Is.False, "loading alone writes nothing");
        }

        [TestCase("{\"version\":1,\"stacks\":[{\"itemId\":\"" + _herbId + "\",\"count\":0}]}", "has no positive count")]
        [TestCase("not json", "is not valid JSON")]
        [TestCase("{\"version\":99,\"stacks\":[]}", "unknown version")]
        public void ABrokenFileIsMovedAsideAndTheStashStartsAfresh(string text, string problem)
        {
            var path = _store.PathOf(_account);
            File.WriteAllText(path, text);

            var loaded = _store.Load(_account, Spec(new StashEntry(_rifleItemId, 1)), Items());

            Assert.That(loaded.Stacks.Select(s => (s.ItemId, s.Count)), Is.EqualTo(new[] { (_rifleItemId, 1) }));
            Assert.That(File.Exists(path), Is.False);
            Assert.That(File.ReadAllText(_store.BrokenPathOf(_account)), Is.EqualTo(text), "the unreadable text is kept to mend by hand");
            Assert.That(_store.BrokenPathOf(_account), Is.EqualTo(path + ".broken"));
            Assert.That(_log.ToString(), Does.Contain("[Stash] saved stash '" + path + "': ").And.Contain(problem)
                .And.Contain("kept the text in '" + path + ".broken'"));

            // 次の保存は新しい中身で書き、退けたファイルはそのまま残る
            _store.Save(_account, loaded);
            Assert.That(_store.Load(_account, Spec(), Items()).CountOf(_rifleItemId), Is.EqualTo(1));
            Assert.That(File.ReadAllText(_store.BrokenPathOf(_account)), Is.EqualTo(text));
        }

        [Test]
        public void AStashOverTheCapacityIsKeptWhole()
        {
            var big = Stash.Fresh(new StashSpec(capacity: 3), Items());
            big.Add(_herbId, 15);
            _store.Save(_account, big);

            var loaded = _store.Load(_account, new StashSpec(capacity: 2), Items());

            Assert.That(loaded.CountOf(_herbId), Is.EqualTo(15));
            Assert.That(_log.ToString(), Does.Contain("[Stash] saved stash '" + _store.PathOf(_account) + "' has 3 stacks, more than the capacity 2; keeping them all"));
        }

        [TestCase("0123456789abcdef0123456789abcdef", true)]
        [TestCase("device-1_a", true)]
        [TestCase("bot.1000", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase(".hidden", false)]
        [TestCase("..", false)]
        [TestCase("-dash", false)]
        [TestCase("a/b", false)]
        [TestCase("a\\b", false)]
        [TestCase("Upper", false)]
        [TestCase("with space", false)]
        public void AccountIdsAreFileNamesWithoutPaths(string accountId, bool valid)
        {
            Assert.That(ServerStashStore.IsAccountId(accountId), Is.EqualTo(valid));
            if (!valid)
            {
                Assert.Throws<ArgumentException>(() => _store.PathOf(accountId));
            }
        }

        [Test]
        public void AnAccountIdLongerThanTheLimitIsRefused()
        {
            Assert.That(ServerStashStore.IsAccountId(new string('a', ServerStashStore.MaxAccountIdLength)), Is.True);
            Assert.That(ServerStashStore.IsAccountId(new string('a', ServerStashStore.MaxAccountIdLength + 1)), Is.False);
        }

        [Test]
        public void AFileWhereTheDirectoryShouldBeCannotBeOpened()
        {
            var file = Path.Combine(_root.Path, "not-a-directory");
            File.WriteAllText(file, "x");

            Assert.That(ServerStashStore.TryOpen(file, _log, out var store, out var error), Is.False);
            Assert.That(store, Is.Null);
            Assert.That(error, Does.StartWith("cannot use the stash directory " + file + ": "));
            Assert.That(ServerStashStore.TryOpen(" ", _log, out _, out error), Is.False);
            Assert.That(error, Is.EqualTo("the stash directory is empty"));
        }

        [Test]
        public void TheArgumentsAreChecked()
        {
            Assert.Throws<ArgumentNullException>(() => ServerStashStore.TryOpen(_root.Path, null, out _, out _));
            Assert.Throws<ArgumentNullException>(() => _store.Load(_account, null, Items()));
            Assert.Throws<ArgumentNullException>(() => _store.Load(_account, Spec(), null));
            Assert.Throws<ArgumentNullException>(() => _store.Save(_account, null));
            Assert.Throws<ArgumentException>(() => _store.Save("../escape", Stash.Fresh(Spec(), Items())));
        }

        private static StashSpec Spec(params StashEntry[] starting)
        {
            return new StashSpec(capacity: 10, startingItems: starting);
        }

        // 薬草は 5 個で 1 山、小銃は 1 つで 1 山
        private static ItemCatalog Items()
        {
            return new ItemCatalog(
                new[]
                {
                    new ItemSpec(_herbId, "item.herb", maxStack: 5),
                    new ItemSpec(_rifleItemId, "weapon.rifle", weaponId: TestRules.RifleId),
                },
                InventoryRule.Default);
        }
    }
}

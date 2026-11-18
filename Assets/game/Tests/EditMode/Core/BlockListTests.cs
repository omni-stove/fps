using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BlockListTests
    {
        private const string _alice = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _bob = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

        private static readonly string[] _aliceOnly = { _alice };
        private static readonly string[] _bobOnly = { _bob };
        private static readonly string[] _aliceAndBob = { _alice, _bob };
        private static readonly string[] _rootNotObject = { "the root must be an object" };
        private static readonly string[] _unsupportedVersion = { "unsupported version (expected 1 to 1)" };

        [Test]
        public void ANewListBlocksAndMutesNobody()
        {
            var list = new BlockList();

            Assert.That(list.IsBlocked(_alice), Is.False);
            Assert.That(list.IsMuted(_alice), Is.False);
            Assert.That(list.BlockedCount, Is.Zero);
            Assert.That(list.MutedCount, Is.Zero);
        }

        [Test]
        public void BlockingAndMutingAreSeparate()
        {
            var list = new BlockList();

            Assert.That(list.SetBlocked(_alice, true), Is.True);
            Assert.That(list.SetMuted(_bob, true), Is.True);

            Assert.That(list.IsBlocked(_alice), Is.True);
            Assert.That(list.IsMuted(_alice), Is.False);
            Assert.That(list.IsBlocked(_bob), Is.False);
            Assert.That(list.IsMuted(_bob), Is.True);
        }

        [Test]
        public void SettingTheSameValueAgainChangesNothingAndDoesNotNotify()
        {
            var list = new BlockList();
            var heard = 0;
            list.Changed += () => heard++;

            Assert.That(list.SetBlocked(_alice, true), Is.True);
            Assert.That(list.SetBlocked(_alice, true), Is.False);
            Assert.That(list.SetBlocked(_bob, false), Is.False);
            Assert.That(list.SetBlocked(_alice, false), Is.True);

            Assert.That(heard, Is.EqualTo(2));
            Assert.That(list.IsBlocked(_alice), Is.False);
        }

        [Test]
        public void UserIdsMatchIgnoringCaseLikeTheVoiceRoster()
        {
            var list = new BlockList();

            list.SetMuted("ABCDEF0123", true);

            Assert.That(list.IsMuted("abcdef0123"), Is.True);
            Assert.That(list.SetMuted("abcdef0123", true), Is.False);
            Assert.That(list.MutedCount, Is.EqualTo(1));
        }

        [Test]
        public void AnInvalidUserIdIsRejectedOnSetAndNeverMatches()
        {
            var list = new BlockList();

            Assert.Throws<ArgumentException>(() => list.SetBlocked(null, true));
            Assert.Throws<ArgumentException>(() => list.SetBlocked(string.Empty, true));
            Assert.Throws<ArgumentException>(() => list.SetMuted("has space", true));
            Assert.Throws<ArgumentException>(() => list.SetMuted("tab\tinside", true));
            Assert.That(list.IsBlocked(null), Is.False);
            Assert.That(list.IsMuted(string.Empty), Is.False);
        }

        [Test]
        public void JsonRoundTripsInASortedOrder()
        {
            var list = new BlockList();
            list.SetBlocked(_bob, true);
            list.SetBlocked(_alice, true);
            list.SetMuted(_bob, true);

            var json = list.ToJson();

            Assert.That(json, Is.EqualTo("{\"version\":1,\"blocked\":[\"" + _alice + "\",\"" + _bob + "\"],\"muted\":[\"" + _bob + "\"]}"));
            var read = BlockList.FromJson(json, out var problems);
            Assert.That(problems, Is.Empty);
            Assert.That(read.Blocked, Is.EqualTo(_aliceAndBob));
            Assert.That(read.Muted, Is.EqualTo(_bobOnly));
        }

        [Test]
        public void AnEmptyListWritesEmptyArrays()
        {
            Assert.That(new BlockList().ToJson(), Is.EqualTo("{\"version\":1,\"blocked\":[],\"muted\":[]}"));
        }

        [Test]
        public void BrokenJsonGivesAnEmptyListAndSaysWhy()
        {
            var list = BlockList.FromJson("{not json", out var problems);

            Assert.That(list.BlockedCount, Is.Zero);
            Assert.That(problems, Has.Count.EqualTo(1));
            Assert.That(problems[0], Does.StartWith("not valid JSON"));
        }

        [Test]
        public void ARootThatIsNotAnObjectIsRejected()
        {
            BlockList.FromJson("[]", out var problems);

            Assert.That(problems, Is.EqualTo(_rootNotObject));
        }

        [TestCase("{\"blocked\":[\"" + _alice + "\"]}")]
        [TestCase("{\"version\":2,\"blocked\":[\"" + _alice + "\"]}")]
        [TestCase("{\"version\":\"1\",\"blocked\":[\"" + _alice + "\"]}")]
        public void AnUnsupportedVersionIsNotRead(string json)
        {
            var list = BlockList.FromJson(json, out var problems);

            Assert.That(list.BlockedCount, Is.Zero);
            Assert.That(problems, Is.EqualTo(_unsupportedVersion));
        }

        [Test]
        public void BadEntriesAreSkippedAndTheRestIsKept()
        {
            var json = "{\"version\":1,\"blocked\":[\"" + _alice + "\",3,\"\",\"" + _alice.ToUpperInvariant() + "\"],\"muted\":\"" + _bob + "\",\"extra\":true}";

            var list = BlockList.FromJson(json, out var problems);

            Assert.That(list.Blocked, Is.EqualTo(_aliceOnly));
            Assert.That(list.MutedCount, Is.Zero);
            Assert.That(problems, Is.EqualTo(new List<string>
            {
                "ignored unknown key 'extra'",
                "'blocked'[1] is not a user id; skipped",
                "'blocked'[2] is not a user id; skipped",
                "'blocked'[3] repeats '" + _alice.ToUpperInvariant() + "'; skipped",
                "'muted' must be an array of user ids",
            }));
        }

        [Test]
        public void MissingArraysAreEmpty()
        {
            var list = BlockList.FromJson("{\"version\":1}", out var problems);

            Assert.That(problems, Is.Empty);
            Assert.That(list.BlockedCount, Is.Zero);
            Assert.That(list.MutedCount, Is.Zero);
        }

        [Test]
        public void FromJsonRejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => BlockList.FromJson(null, out _));
        }

        [Test]
        public void TheReadListsAreSnapshots()
        {
            var list = new BlockList();
            list.SetBlocked(_alice, true);
            var before = list.Blocked;

            list.SetBlocked(_bob, true);

            Assert.That(before, Is.EqualTo(_aliceOnly));
            Assert.That(list.Blocked, Is.EqualTo(_aliceAndBob));
        }
    }
}

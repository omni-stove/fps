using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientBlocksTests
    {
        private const string _self = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const string _other = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        private const int _selfId = 1;
        private const int _otherId = 2;
        private const int _unlistedId = 9;

        private static readonly string[] _otherOnly = { _other };

        private readonly List<string> _logs = new List<string>();
        private BlockList _list;
        private VoiceRoster _roster;
        private int _saves;
        private int _changes;
        private ClientBlocks _blocks;

        [SetUp]
        public void SetUp()
        {
            _logs.Clear();
            _saves = 0;
            _changes = 0;
            _list = new BlockList();
            _roster = new VoiceRoster();
            _roster.Set(_self, _selfId);
            _roster.Set(_other, _otherId);
            _blocks = new ClientBlocks(_list, _roster, () => _selfId, _ => _saves++, _logs.Add);
            _blocks.Changed += () => _changes++;
        }

        [TearDown]
        public void TearDown()
        {
            _blocks.Dispose();
        }

        [Test]
        public void BlockingSomeoneOnTheRosterIsRememberedByTheirUserIdAndSaved()
        {
            Assert.That(_blocks.SetBlocked(_otherId, true), Is.True);

            Assert.That(_blocks.IsBlocked(_otherId), Is.True);
            Assert.That(_blocks.IsMuted(_otherId), Is.False);
            Assert.That(_list.Blocked, Is.EqualTo(_otherOnly));
            Assert.That(_saves, Is.EqualTo(1));
            Assert.That(_changes, Is.EqualTo(1));
            Assert.That(_logs, Is.EqualTo(new[] { "[Block] block=on participant=2 saved" }));
        }

        [Test]
        public void MutingIsRememberedSeparately()
        {
            _blocks.SetMuted(_otherId, true);

            Assert.That(_blocks.IsMuted(_otherId), Is.True);
            Assert.That(_blocks.IsMutedUser(_other), Is.True);
            Assert.That(_blocks.IsBlocked(_otherId), Is.False);
            Assert.That(_list.Muted, Is.EqualTo(_otherOnly));
            Assert.That(_list.BlockedCount, Is.Zero);
        }

        [Test]
        public void TheSameUserGetsANewParticipantIdInTheNextMatch()
        {
            _blocks.SetBlocked(_otherId, true);

            _roster.Clear();
            _roster.Set(_other, 5);

            Assert.That(_blocks.IsBlocked(5), Is.True);
            Assert.That(_blocks.IsBlocked(_otherId), Is.False, "someone else may have the old id now");
        }

        [Test]
        public void SettingTheSameValueAgainChangesNothing()
        {
            _blocks.SetBlocked(_otherId, true);

            Assert.That(_blocks.SetBlocked(_otherId, true), Is.False);

            Assert.That(_saves, Is.EqualTo(1));
            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void UnblockingRemovesTheSavedEntry()
        {
            _blocks.SetBlocked(_otherId, true);

            Assert.That(_blocks.SetBlocked(_otherId, false), Is.True);

            Assert.That(_blocks.IsBlocked(_otherId), Is.False);
            Assert.That(_list.BlockedCount, Is.Zero);
            Assert.That(_saves, Is.EqualTo(2));
            Assert.That(_logs[_logs.Count - 1], Is.EqualTo("[Block] block=off participant=2 saved"));
        }

        [Test]
        public void SomeoneOffTheRosterIsBlockedForThisMatchOnly()
        {
            Assert.That(_blocks.SetBlocked(_unlistedId, true), Is.True);

            Assert.That(_blocks.IsBlocked(_unlistedId), Is.True);
            Assert.That(_list.BlockedCount, Is.Zero);
            Assert.That(_saves, Is.Zero);
            Assert.That(_logs, Is.EqualTo(new[] { "[Block] block=on participant=9 this match only (no user id for the participant)" }));
        }

        [Test]
        public void AMatchOnlyBlockIsSavedOnceTheRosterNamesThePlayer()
        {
            _blocks.SetMuted(_unlistedId, true);
            const string late = "cccccccccccccccccccccccccccccccc";

            _roster.Set(late, _unlistedId);

            Assert.That(_list.IsMuted(late), Is.True);
            Assert.That(_blocks.IsMuted(_unlistedId), Is.True);
            Assert.That(_saves, Is.EqualTo(1));
            Assert.That(_logs, Has.Member("[Block] mute participant=9 now has a user id; saved"));
        }

        [Test]
        public void UnblockingAMatchOnlyBlockNeedsNoSave()
        {
            _blocks.SetBlocked(_unlistedId, true);

            Assert.That(_blocks.SetBlocked(_unlistedId, false), Is.True);

            Assert.That(_blocks.IsBlocked(_unlistedId), Is.False);
            Assert.That(_saves, Is.Zero);
        }

        [Test]
        public void TheLocalPlayerCannotBeBlockedOrMuted()
        {
            Assert.That(_blocks.SetBlocked(_selfId, true), Is.False);
            Assert.That(_blocks.SetMuted(_selfId, true), Is.False);

            Assert.That(_blocks.IsBlocked(_selfId), Is.False);
            Assert.That(_list.BlockedCount, Is.Zero);
            Assert.That(_changes, Is.Zero);
            Assert.That(_logs, Is.EqualTo(new[]
            {
                "[Block] block refused participant=1 (the local player)",
                "[Block] mute refused participant=1 (the local player)",
            }));
        }

        [Test]
        public void ANegativeParticipantIdIsRefused()
        {
            Assert.That(_blocks.SetBlocked(-1, true), Is.False);

            Assert.That(_logs, Is.EqualTo(new[] { "[Block] block refused participant=-1 (not a participant id)" }));
        }

        [Test]
        public void TwoUserIdsWithTheSameParticipantIdAreNotGuessed()
        {
            _roster.Set("dddddddddddddddddddddddddddddddd", _otherId);

            Assert.That(_blocks.TryGetUserId(_otherId, out var userId), Is.False);
            Assert.That(userId, Is.Null);

            _blocks.SetBlocked(_otherId, true);

            Assert.That(_list.BlockedCount, Is.Zero, "kept for this match only");
            Assert.That(_blocks.IsBlocked(_otherId), Is.True);
        }

        [Test]
        public void AFailedSaveIsLoggedAndTheBlockStillHolds()
        {
            var blocks = new ClientBlocks(new BlockList(), _roster, () => _selfId, _ => throw new InvalidOperationException("disk full"), _logs.Add);

            blocks.SetBlocked(_otherId, true);

            Assert.That(blocks.IsBlocked(_otherId), Is.True);
            Assert.That(_logs, Has.Member("[Block] could not save the block list: disk full"));
            blocks.Dispose();
        }

        [Test]
        public void ARosterChangeIsPassedOnSoListenersRecheck()
        {
            var message = new VoiceRosterMessage();
            message.Add(_otherId, _other);

            _roster.Apply(message);

            Assert.That(_changes, Is.EqualTo(1));
        }

        [Test]
        public void AfterDisposingTheRosterIsNoLongerWatchedAndSettingThrows()
        {
            _blocks.Dispose();

            _roster.Set("eeeeeeeeeeeeeeeeeeeeeeeeeeeeeeee", 4);

            Assert.That(_changes, Is.Zero);
            Assert.Throws<ObjectDisposedException>(() => _blocks.SetBlocked(_otherId, true));
        }

        [Test]
        public void TheConstructorRejectsMissingPieces()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientBlocks(null, _roster, () => 0, _ => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ClientBlocks(_list, null, () => 0, _ => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ClientBlocks(_list, _roster, null, _ => { }, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ClientBlocks(_list, _roster, () => 0, null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => new ClientBlocks(_list, _roster, () => 0, _ => { }, null));
        }
    }
}

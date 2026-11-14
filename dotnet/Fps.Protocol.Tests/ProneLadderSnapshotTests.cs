using System;
using System.Numerics;
using Fps.Core;
using NUnit.Framework;

namespace Fps.Protocol.Tests
{
    public sealed class ProneLadderSnapshotTests
    {
        private readonly MessageTable _clientTable = MatchMessages.CreateClientTable();
        private readonly MessageTable _serverTable = MatchMessages.CreateServerTable();

        [Test]
        public void ProneAndLadderStatesRoundTrip()
        {
            var sent = Snapshot(10, prone: new CharacterProneState(true, true, false, true, new Vector2(0.6f, 0.8f), 0.25f),
                ladder: CharacterLadderState.Climbing(2, LadderEntry.Top, 1.75f, true, -1), isProne: true);

            var got = Send(sent, null).Players[0];

            Assert.That(got.IsProne, Is.True);
            Assert.That(got.IsCrouched, Is.False);
            Assert.That(got.Prone, Is.EqualTo(sent.Players[0].Prone));
            Assert.That(got.Ladder, Is.EqualTo(sent.Players[0].Ladder));
        }

        [Test]
        public void ALadderLeftWithARegrabWaitRoundTrips()
        {
            var sent = Snapshot(10, ladder: CharacterLadderState.Off(0.3f));

            var got = Send(sent, null).Players[0];

            Assert.That(got.Ladder.IsClimbing, Is.False);
            Assert.That(got.Ladder.RegrabRemaining, Is.EqualTo(0.3f));
        }

        [Test]
        public void OnlyTheChangedPartsAreSent()
        {
            var baseline = Snapshot(10);
            var climbing = Snapshot(11, ladder: CharacterLadderState.Climbing(0, LadderEntry.Front, 0.5f, false, 0));
            Send(baseline, null);

            var got = Send(climbing, baseline).Players[0];

            Assert.That(got.Present, Is.EqualTo(PlayerFields.Ladder));
            got.FillFrom(baseline.Players[0]);
            Assert.That(got.Ladder, Is.EqualTo(climbing.Players[0].Ladder));
        }

        [Test]
        public void CrouchedAndProneTogetherIsRefused()
        {
            var sent = Snapshot(10, isProne: true);
            sent.Players[0].IsCrouched = true;

            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(sent));
        }

        [Test]
        public void TheProneButtonTravelsWithTheInput()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(3, Vector2.Zero, 0f, InputButtons.Prone | InputButtons.Sprint));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            var got = (InputMessage)_serverTable.Read(writer.AsSegment());

            Assert.That(got[0].Has(InputButtons.Prone), Is.True);
            Assert.That(got[0].Has(InputButtons.Sprint), Is.True);
        }

        private static SnapshotMessage Snapshot(
            int tick, CharacterProneState prone = default, CharacterLadderState ladder = default, bool isProne = false)
        {
            var snapshot = new SnapshotMessage { Tick = tick };
            ref var player = ref snapshot.Players.Add();
            player.PlayerId = 1;
            player.Position = new Vector3(1f, 2f, 3f);
            player.IsGrounded = true;
            player.IsProne = isProne;
            player.WeaponId = "rifle";
            player.Prone = prone;
            player.Ladder = ladder;
            return snapshot;
        }

        private SnapshotMessage Send(SnapshotMessage message, SnapshotMessage baseline)
        {
            message.Baseline = baseline;
            var writer = new MessageWriter();
            writer.WriteMessage(message);
            message.Baseline = null;
            var copy = new SnapshotMessage();
            copy.CopyFrom((SnapshotMessage)_clientTable.Read(writer.AsSegment()));
            return copy;
        }
    }
}

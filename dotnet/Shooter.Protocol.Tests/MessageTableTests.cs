using System;
using System.Linq;
using System.Numerics;
using System.Reflection;
using NUnit.Framework;

namespace Shooter.Protocol.Tests
{
    public sealed class MessageTableTests
    {
        [Test]
        public void EveryMessageIdIsReceivedByExactlyOneSide()
        {
            var declared = typeof(MessageIds)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => (ushort)f.GetValue(null))
                .ToList();
            var received = MatchMessages.CreateClientToServer().Concat(MatchMessages.CreateServerToClient()).Select(m => m.Id).ToList();

            Assert.That(declared, Is.Unique);
            Assert.That(received, Is.Unique);
            Assert.That(received, Is.EquivalentTo(declared));
        }

        [Test]
        public void EveryMessageHasAVersion()
        {
            foreach (var message in MatchMessages.CreateClientToServer().Concat(MatchMessages.CreateServerToClient()))
            {
                Assert.That(message.Version, Is.GreaterThan(0), message.GetType().Name);
            }
        }

        [Test]
        public void DuplicateIdsAreRejected()
        {
            Assert.Throws<ArgumentException>(
                () => new MessageTable(new IMessage[] { new PingMovedMessage(), new PingMovedMessage() }));
        }

        [Test]
        public void UnknownIdIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(60000);
            writer.WriteByte(1);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));
        }

        [Test]
        public void MessageForTheOtherSideIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ConnectAcceptedMessage { PlayerId = 1, ServerVersion = "1", TickRate = 60 });

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
        }

        [Test]
        public void OtherVersionIsRejectedWithoutReadingTheBody()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.ReviveRequest);
            writer.WriteByte(ReviveRequestMessage.CurrentVersion + 1);
            writer.WriteInt32(3);
            var table = MatchMessages.CreateServerTable();

            var error = Assert.Throws<MessageReadException>(() => table.Read(writer.AsSegment()));

            Assert.That(error.Message, Does.Contain("version"));
            Assert.That(table.TryGet(MessageIds.ReviveRequest, out var revive), Is.True);
            Assert.That(((ReviveRequestMessage)revive).TargetPlayerId, Is.Zero);
        }

        [Test]
        public void TrailingBytesAreRejected()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new ReviveRequestMessage { TargetPlayerId = 1 });
            writer.WriteByte(0);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
        }

        [Test]
        public void TruncatedMessageIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new FireRequestMessage { WeaponId = "rifle" });
            var bytes = writer.ToArray();

            Assert.Throws<MessageReadException>(
                () => MatchMessages.CreateServerTable().Read(new ArraySegment<byte>(bytes, 0, bytes.Length - 1)));
        }

        [Test]
        public void ReadReusesOneInstancePerKind()
        {
            var table = MatchMessages.CreateServerTable();
            var writer = new MessageWriter();
            writer.WriteMessage(new ReviveRequestMessage { TargetPlayerId = 1 });
            var first = table.Read(writer.AsSegment());
            writer.Reset();
            writer.WriteMessage(new ReviveRequestMessage { TargetPlayerId = 2 });

            var second = table.Read(writer.AsSegment());

            Assert.That(second, Is.SameAs(first));
            Assert.That(((ReviveRequestMessage)second).TargetPlayerId, Is.EqualTo(2));
        }

        [Test]
        public void InputTicksMustFollowEachOther()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(10, Vector2.Zero, 0f, InputButtons.None));

            Assert.Throws<InvalidOperationException>(() => input.Add(new InputFrame(12, Vector2.Zero, 0f, InputButtons.None)));
        }

        [Test]
        public void InputHoldsAtMostMaxFrames()
        {
            var input = new InputMessage();
            for (var i = 0; i < InputMessage.MaxFrames; i++)
            {
                input.Add(new InputFrame(i, Vector2.Zero, 0f, InputButtons.None));
            }

            Assert.Throws<InvalidOperationException>(
                () => input.Add(new InputFrame(InputMessage.MaxFrames, Vector2.Zero, 0f, InputButtons.None)));
        }

        [Test]
        public void EmptyInputIsNotWritten()
        {
            Assert.Throws<InvalidOperationException>(() => new MessageWriter().WriteMessage(new InputMessage()));
        }

        [Test]
        public void NonFiniteInputIsRejected()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, new Vector2(float.NaN, 0f), 0f, InputButtons.None));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
        }

        [Test]
        public void UnknownInputButtonIsRejected()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, Vector2.Zero, 0f, (InputButtons)1024));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
        }

        [Test]
        public void TheMeleeButtonIsReadByTheServer()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, Vector2.Zero, 0f, InputButtons.Melee | InputButtons.Crouch));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            var got = (InputMessage)MatchMessages.CreateServerTable().Read(writer.AsSegment());

            Assert.That((int)InputButtons.Melee, Is.EqualTo(512));
            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.Melee | InputButtons.Crouch));
            Assert.That(InputButtons.All & InputButtons.Melee, Is.EqualTo(InputButtons.Melee));
        }

        [Test]
        public void InputButtonsAreWrittenInTwoBytes()
        {
            var one = new InputMessage();
            one.Add(new InputFrame(1, Vector2.Zero, 0f, InputButtons.LeanRight));
            var writer = new MessageWriter();
            writer.WriteMessage(one);

            // Id・版 (2 + 1)、受け取ったスナップショットの tick・数・最初の tick (4 + 1 + 4)、移動・向き (8 + 4)、ボタン (2)
            Assert.That(writer.Length, Is.EqualTo(3 + 9 + 12 + 2));
        }

        [TestCase(MessageIds.ChatRequest)]
        [TestCase(MessageIds.MapVoteRequest)]
        [TestCase(MessageIds.SpectateRequest)]
        [TestCase(MessageIds.StreakUseRequest)]
        [TestCase(MessageIds.DeployRequest)]
        [TestCase(MessageIds.ReportRequest)]
        [TestCase(MessageIds.VoteKickRequest)]
        [TestCase(MessageIds.BuyRequest)]
        public void TheNewRequestsAreOnlyReadByTheServer(ushort id)
        {
            Assert.That(MatchMessages.CreateServerTable().TryGet(id, out _), Is.True);
            Assert.That(MatchMessages.CreateClientTable().TryGet(id, out _), Is.False);
        }

        [TestCase(MessageIds.Chat)]
        [TestCase(MessageIds.MapVoteState)]
        [TestCase(MessageIds.VoiceRoster)]
        [TestCase(MessageIds.StreakActivated)]
        [TestCase(MessageIds.KillCamClip)]
        [TestCase(MessageIds.VoteKickState)]
        public void TheNewServerMessagesAreOnlyReadByTheClient(ushort id)
        {
            Assert.That(MatchMessages.CreateClientTable().TryGet(id, out _), Is.True);
            Assert.That(MatchMessages.CreateServerTable().TryGet(id, out _), Is.False);
        }

        [Test]
        public void TheVoteKickAndBuyMessagesHaveTheirIds()
        {
            Assert.That(MessageIds.VoteKickRequest, Is.EqualTo(33));
            Assert.That(MessageIds.BuyRequest, Is.EqualTo(34));
            Assert.That(MessageIds.VoteKickState, Is.EqualTo(52));
            Assert.That(new VoteKickRequestMessage().Id, Is.EqualTo(MessageIds.VoteKickRequest));
            Assert.That(new BuyRequestMessage().Id, Is.EqualTo(MessageIds.BuyRequest));
            Assert.That(new VoteKickStateMessage().Id, Is.EqualTo(MessageIds.VoteKickState));
        }

        [Test]
        public void TheTablesReadTheVoteKickAndBuyMessagesAsTheirTypes()
        {
            Assert.That(MatchMessages.CreateServerTable().TryGet(MessageIds.VoteKickRequest, out var voteKick), Is.True);
            Assert.That(voteKick, Is.InstanceOf<VoteKickRequestMessage>());
            Assert.That(MatchMessages.CreateServerTable().TryGet(MessageIds.BuyRequest, out var buy), Is.True);
            Assert.That(buy, Is.InstanceOf<BuyRequestMessage>());
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.VoteKickState, out var state), Is.True);
            Assert.That(state, Is.InstanceOf<VoteKickStateMessage>());
        }

        [Test]
        public void ContentIdListStopsAtItsLimit()
        {
            var list = new ContentIdList(1, "weapon");
            list.Add("a");

            Assert.Throws<InvalidOperationException>(() => list.Add("b"));
        }
    }
}

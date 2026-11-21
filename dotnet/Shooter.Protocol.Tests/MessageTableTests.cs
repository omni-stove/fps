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
            Assert.That(received, Is.SubsetOf(declared));
            Assert.That(declared.Except(received).Where(id => !MessageTable.IsReserved(id)), Is.Empty,
                "an id that no side reads must be one of the reserved ids");
        }

        [Test]
        public void TheEmotePickupAndDroneIdsAreReservedAfterTheInteractRequest()
        {
            Assert.That(MessageIds.EmoteRequest, Is.EqualTo(36));
            Assert.That(MessageIds.PickupRequest, Is.EqualTo(37));
            Assert.That(MessageIds.DroneControl, Is.EqualTo(38));
            Assert.That(MessageTable.IsReserved(MessageIds.EmoteRequest), Is.True);
            Assert.That(MessageTable.IsReserved(MessageIds.PickupRequest), Is.True);
            Assert.That(MessageTable.IsReserved(MessageIds.DroneControl), Is.True);
            Assert.That(MessageTable.IsReserved(MessageIds.InteractRequest), Is.False);
            Assert.That(MessageTable.IsReserved(60000), Is.False);
        }

        // 予約した Id は前の版と同じく読まずに断るが、知らない Id とは理由を分ける。拾う要求は型を足したので、もう断らない
        [TestCase(MessageIds.EmoteRequest)]
        [TestCase(MessageIds.DroneControl)]
        public void AReservedIdIsRejectedByBothSidesUntilItsMessageIsAdded(ushort id)
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(id);
            writer.WriteByte(1);

            var server = Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
            var client = Assert.Throws<MessageReadException>(() => MatchMessages.CreateClientTable().Read(writer.AsSegment()));

            Assert.That(server.Message, Does.Contain("message id " + id + " is reserved"));
            Assert.That(client.Message, Does.Contain("message id " + id + " is reserved"));
        }

        [Test]
        public void AnUnknownIdIsNotCalledReserved()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(60000);
            writer.WriteByte(1);

            var error = Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));

            Assert.That(error.Message, Does.Contain("message id 60000 is not accepted here"));
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
        public void AnOlderVersionIsRejectedUnlessTheMessageReadsOlderVersions()
        {
            Assert.That(new ReviveRequestMessage(), Is.Not.InstanceOf<IReadsOlderVersions>());
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.ReviveRequest);
            writer.WriteByte(ReviveRequestMessage.CurrentVersion - 1);
            writer.WriteInt32(3);

            var error = Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));

            Assert.That(error.Message, Does.Contain("version"));
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
            input.Add(new InputFrame(1, Vector2.Zero, 0f, (InputButtons)32768));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
        }

        [Test]
        public void TheParachuteButtonsAreReadByTheServer()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, Vector2.Zero, 0f, InputButtons.OpenParachute | InputButtons.Sprint));
            input.Add(new InputFrame(2, Vector2.Zero, 0f, InputButtons.CloseParachute));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            var got = (InputMessage)MatchMessages.CreateServerTable().Read(writer.AsSegment());

            Assert.That((int)InputButtons.OpenParachute, Is.EqualTo(1024));
            Assert.That((int)InputButtons.CloseParachute, Is.EqualTo(2048));
            Assert.That(InputMessage.CurrentVersion, Is.EqualTo(6));
            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.OpenParachute | InputButtons.Sprint));
            Assert.That(got[1].Buttons, Is.EqualTo(InputButtons.CloseParachute));
            Assert.That(InputButtons.All & (InputButtons.OpenParachute | InputButtons.CloseParachute),
                Is.EqualTo(InputButtons.OpenParachute | InputButtons.CloseParachute));
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
        public void TheSlideGrappleAndTakedownButtonsAreReadByTheServer()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, Vector2.Zero, 0f, InputButtons.Slide | InputButtons.Sprint));
            input.Add(new InputFrame(2, Vector2.Zero, 0f, InputButtons.Grapple));
            input.Add(new InputFrame(3, Vector2.Zero, 0f, InputButtons.Takedown | InputButtons.Crouch));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            var got = (InputMessage)MatchMessages.CreateServerTable().Read(writer.AsSegment());

            Assert.That((int)InputButtons.Slide, Is.EqualTo(4096));
            Assert.That((int)InputButtons.Grapple, Is.EqualTo(8192));
            Assert.That((int)InputButtons.Takedown, Is.EqualTo(16384));
            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.Slide | InputButtons.Sprint));
            Assert.That(got[1].Buttons, Is.EqualTo(InputButtons.Grapple));
            Assert.That(got[2].Buttons, Is.EqualTo(InputButtons.Takedown | InputButtons.Crouch));
        }

        [Test]
        public void TheExistingButtonsKeepTheirValuesAndAllHasEveryButton()
        {
            Assert.That((int)InputButtons.Sprint, Is.EqualTo(1));
            Assert.That((int)InputButtons.Jump, Is.EqualTo(2));
            Assert.That((int)InputButtons.Crouch, Is.EqualTo(4));
            Assert.That((int)InputButtons.Cover, Is.EqualTo(8));
            Assert.That((int)InputButtons.Aim, Is.EqualTo(16));
            Assert.That((int)InputButtons.Dodge, Is.EqualTo(32));
            Assert.That((int)InputButtons.Prone, Is.EqualTo(64));
            Assert.That((int)InputButtons.LeanLeft, Is.EqualTo(128));
            Assert.That((int)InputButtons.LeanRight, Is.EqualTo(256));
            Assert.That((int)InputButtons.Melee, Is.EqualTo(512));
            Assert.That((int)InputButtons.OpenParachute, Is.EqualTo(1024));
            Assert.That((int)InputButtons.CloseParachute, Is.EqualTo(2048));
            Assert.That((int)InputButtons.All, Is.EqualTo(32767));
            Assert.That(InputMessage.CurrentVersion, Is.EqualTo(6));
        }

        [Test]
        public void InputButtonsAreWrittenInTwoBytes()
        {
            var one = new InputMessage();
            one.Add(new InputFrame(1, Vector2.Zero, 0f, InputButtons.LeanRight));
            var writer = new MessageWriter();
            writer.WriteMessage(one);

            // Id・版 (2 + 1)、受け取ったスナップショットの tick・数・最初の tick (4 + 1 + 4)、移動・横と縦の向き (8 + 4 + 4)、ボタン (2)
            Assert.That(writer.Length, Is.EqualTo(3 + 9 + 16 + 2));
        }

        [Test]
        public void ThePitchIsReadByTheServerAndDefaultsToLevel()
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, Vector2.Zero, 0.5f, InputButtons.Grapple, 0.75f));
            input.Add(new InputFrame(2, Vector2.Zero, 0.5f, InputButtons.None, -InputFrame.MaxPitch));
            input.Add(new InputFrame(3, Vector2.Zero, 0.5f, InputButtons.None));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            var got = (InputMessage)MatchMessages.CreateServerTable().Read(writer.AsSegment());

            Assert.That(got[0].Pitch, Is.EqualTo(0.75f));
            Assert.That(got[0].Yaw, Is.EqualTo(0.5f));
            Assert.That(got[0].Buttons, Is.EqualTo(InputButtons.Grapple));
            Assert.That(got[1].Pitch, Is.EqualTo(-InputFrame.MaxPitch));
            Assert.That(got[2].Pitch, Is.Zero);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(1.6f)]
        [TestCase(-1.6f)]
        public void APitchBeyondStraightUpOrDownIsRejected(float pitch)
        {
            var input = new InputMessage();
            input.Add(new InputFrame(1, Vector2.Zero, 0f, InputButtons.None, pitch));
            var writer = new MessageWriter();
            writer.WriteMessage(input);

            Assert.Throws<MessageReadException>(() => MatchMessages.CreateServerTable().Read(writer.AsSegment()));
        }

        [TestCase(MessageIds.ChatRequest)]
        [TestCase(MessageIds.MapVoteRequest)]
        [TestCase(MessageIds.SpectateRequest)]
        [TestCase(MessageIds.StreakUseRequest)]
        [TestCase(MessageIds.DeployRequest)]
        [TestCase(MessageIds.ReportRequest)]
        [TestCase(MessageIds.VoteKickRequest)]
        [TestCase(MessageIds.BuyRequest)]
        [TestCase(MessageIds.InteractRequest)]
        [TestCase(MessageIds.PickupRequest)]
        [TestCase(MessageIds.DetonateRequest)]
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
        [TestCase(MessageIds.StashState)]
        [TestCase(MessageIds.BuyAnswer)]
        [TestCase(MessageIds.PlayerNames)]
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
        public void ThePickupRequestKeepsItsReservedIdAndIsReadAsItsType()
        {
            Assert.That(new PickupRequestMessage().Id, Is.EqualTo(MessageIds.PickupRequest));
            Assert.That(MessageTable.IsReserved(MessageIds.PickupRequest), Is.True);
            Assert.That(MatchMessages.CreateServerTable().TryGet(MessageIds.PickupRequest, out var pickup), Is.True);
            Assert.That(pickup, Is.InstanceOf<PickupRequestMessage>());
        }

        [Test]
        public void TheInteractAndStashMessagesHaveTheirIds()
        {
            Assert.That(MessageIds.InteractRequest, Is.EqualTo(35));
            Assert.That(MessageIds.StashState, Is.EqualTo(87));
            Assert.That(new InteractRequestMessage().Id, Is.EqualTo(MessageIds.InteractRequest));
            Assert.That(new StashStateMessage().Id, Is.EqualTo(MessageIds.StashState));
        }

        [Test]
        public void TheTablesReadTheInteractAndStashMessagesAsTheirTypes()
        {
            Assert.That(MatchMessages.CreateServerTable().TryGet(MessageIds.InteractRequest, out var interact), Is.True);
            Assert.That(interact, Is.InstanceOf<InteractRequestMessage>());
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.StashState, out var stash), Is.True);
            Assert.That(stash, Is.InstanceOf<StashStateMessage>());
        }

        [Test]
        public void TheBuyAnswerHasItsIdAndIsReadAsItsType()
        {
            Assert.That(MessageIds.BuyAnswer, Is.EqualTo(88));
            Assert.That(new BuyAnswerMessage().Id, Is.EqualTo(MessageIds.BuyAnswer));
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.BuyAnswer, out var answer), Is.True);
            Assert.That(answer, Is.InstanceOf<BuyAnswerMessage>());
        }

        [Test]
        public void ThePlayerNamesHaveTheLastIdAndAreReadAsTheirType()
        {
            Assert.That(MessageIds.PlayerNames, Is.EqualTo(89));
            Assert.That(new PlayerNamesMessage().Id, Is.EqualTo(MessageIds.PlayerNames));
            Assert.That(MatchMessages.CreateClientTable().TryGet(MessageIds.PlayerNames, out var names), Is.True);
            Assert.That(names, Is.InstanceOf<PlayerNamesMessage>());
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

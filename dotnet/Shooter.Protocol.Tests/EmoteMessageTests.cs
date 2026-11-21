using System;
using NUnit.Framework;
using Shooter.Core;

namespace Shooter.Protocol.Tests
{
    public sealed class EmoteMessageTests
    {
        private readonly MessageTable _server = MatchMessages.CreateServerTable();
        private readonly MessageTable _client = MatchMessages.CreateClientTable();

        [Test]
        public void TheEmoteMessagesHaveTheirIds()
        {
            Assert.That(new EmoteRequestMessage().Id, Is.EqualTo(36));
            Assert.That(new EmoteStateMessage().Id, Is.EqualTo(90));
            Assert.That(MessageIds.EmoteState, Is.EqualTo(90));
            Assert.That(EmoteRequestMessage.CurrentVersion, Is.EqualTo(1));
            Assert.That(EmoteStateMessage.CurrentVersion, Is.EqualTo(1));
        }

        [Test]
        public void APlayRequestRoundTripsWithItsId()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new EmoteRequestMessage { Action = EmoteAction.Play, EmoteId = "wave" });

            var got = (EmoteRequestMessage)_server.Read(writer.AsSegment());

            // Id (2)・版 (1)・動作 (1)・文字列の長さ (2) と "wave" (4)
            Assert.That(writer.Length, Is.EqualTo(3 + 1 + 2 + 4));
            Assert.That(got.Action, Is.EqualTo(EmoteAction.Play));
            Assert.That(got.EmoteId, Is.EqualTo("wave"));
        }

        [Test]
        public void AStopRequestCarriesNoId()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new EmoteRequestMessage { Action = EmoteAction.Stop });

            var got = (EmoteRequestMessage)_server.Read(writer.AsSegment());

            Assert.That(writer.Length, Is.EqualTo(3 + 1));
            Assert.That(got.Action, Is.EqualTo(EmoteAction.Stop));
            Assert.That(got.EmoteId, Is.Empty);
        }

        [Test]
        public void AReadStopClearsTheIdOfTheReusedMessage()
        {
            var play = new MessageWriter();
            play.WriteMessage(new EmoteRequestMessage { Action = EmoteAction.Play, EmoteId = "wave" });
            var stop = new MessageWriter();
            stop.WriteMessage(new EmoteRequestMessage { Action = EmoteAction.Stop });

            _server.Read(play.AsSegment());
            var got = (EmoteRequestMessage)_server.Read(stop.AsSegment());

            Assert.That(got.Action, Is.EqualTo(EmoteAction.Stop));
            Assert.That(got.EmoteId, Is.Empty);
        }

        [Test]
        public void ARequestThatDoesNotMatchItsActionIsNotWritten()
        {
            var writer = new MessageWriter();

            Assert.Throws<InvalidOperationException>(() => writer.WriteMessage(new EmoteRequestMessage { Action = EmoteAction.Play }));
            Assert.Throws<InvalidOperationException>(
                () => writer.WriteMessage(new EmoteRequestMessage { Action = EmoteAction.Stop, EmoteId = "wave" }));
        }

        [Test]
        public void APlayRequestWithAnEmptyIdIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.EmoteRequest);
            writer.WriteByte(EmoteRequestMessage.CurrentVersion);
            writer.WriteByte((byte)EmoteAction.Play);
            writer.WriteString("");

            var e = Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));

            Assert.That(e.Message, Does.Contain("carries no emote id"));
        }

        [Test]
        public void AnUnknownActionIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.EmoteRequest);
            writer.WriteByte(EmoteRequestMessage.CurrentVersion);
            writer.WriteByte(2);

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void AnIdLongerThanAContentIdIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.EmoteRequest);
            writer.WriteByte(EmoteRequestMessage.CurrentVersion);
            writer.WriteByte((byte)EmoteAction.Play);
            writer.WriteString(new string('a', MatchProtocol.MaxContentIdBytes + 1));

            Assert.Throws<MessageReadException>(() => _server.Read(writer.AsSegment()));
        }

        [Test]
        public void APlayingStateRoundTrips()
        {
            var writer = new MessageWriter();
            writer.WriteMessage(new EmoteStateMessage { PlayerId = 3, Playing = true, EmoteId = "salute" });

            var got = (EmoteStateMessage)_client.Read(writer.AsSegment());

            Assert.That(got.PlayerId, Is.EqualTo(3));
            Assert.That(got.Playing, Is.True);
            Assert.That(got.EmoteId, Is.EqualTo("salute"));
            Assert.That(got.StopReason, Is.EqualTo(EmoteStopReason.Finished));
        }

        [TestCase(EmoteStopReason.Finished)]
        [TestCase(EmoteStopReason.Moved)]
        [TestCase(EmoteStopReason.Fired)]
        [TestCase(EmoteStopReason.Riding)]
        public void AStoppedStateRoundTripsWithItsReasonAndNoId(EmoteStopReason reason)
        {
            var playing = new MessageWriter();
            playing.WriteMessage(new EmoteStateMessage { PlayerId = 3, Playing = true, EmoteId = "salute" });
            var stopped = new MessageWriter();
            stopped.WriteMessage(new EmoteStateMessage { PlayerId = 3, Playing = false, StopReason = reason });

            _client.Read(playing.AsSegment());
            var got = (EmoteStateMessage)_client.Read(stopped.AsSegment());

            // Id (2)・版 (1)・人 (4)・始めたか (1)・止めた理由 (1)
            Assert.That(stopped.Length, Is.EqualTo(3 + 4 + 1 + 1));
            Assert.That(got.Playing, Is.False);
            Assert.That(got.EmoteId, Is.Empty);
            Assert.That(got.StopReason, Is.EqualTo(reason));
        }

        [Test]
        public void AStateThatCannotBeReadBackIsNotWritten()
        {
            var writer = new MessageWriter();

            Assert.Throws<InvalidOperationException>(() => writer.WriteMessage(new EmoteStateMessage { PlayerId = -1, Playing = false }));
            Assert.Throws<InvalidOperationException>(() => writer.WriteMessage(new EmoteStateMessage { PlayerId = 1, Playing = true }));
        }

        [Test]
        public void AnUnknownStopReasonIsRejected()
        {
            var writer = new MessageWriter();
            writer.WriteUInt16(MessageIds.EmoteState);
            writer.WriteByte(EmoteStateMessage.CurrentVersion);
            writer.WriteInt32(1);
            writer.WriteBool(false);
            writer.WriteByte((byte)Enum.GetValues<EmoteStopReason>().Length);

            Assert.Throws<MessageReadException>(() => _client.Read(writer.AsSegment()));
        }

        [Test]
        public void TheSyncedRulesCarryTheEmoteRule()
        {
            var defaults = MatchRulesSync.From(GameRules.Default);
            var on = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithEmote(Emote.On)));

            Assert.That(default(MatchRulesSync).Emote, Is.EqualTo(Emote.Off), "no wheel before any rules arrive");
            Assert.That(defaults.Emote, Is.EqualTo(Emote.Off));
            Assert.That(on.Emote, Is.EqualTo(Emote.On));
            Assert.That(MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithEmote(Emote.Off))).SameAs(on), Is.False);
        }

        [TestCase(Emote.On)]
        [TestCase(Emote.Off)]
        public void TheEmoteRuleRoundTripsInTheSnapshot(Emote emote)
        {
            var sent = new SnapshotMessage();
            sent.Clear();
            sent.Tick = 10;
            sent.Match.State = GameModeState.Playing;
            sent.Match.Rules = MatchRulesSync.From(GameRules.Default.WithActions(ActionRules.Default.WithEmote(emote)));
            var writer = new MessageWriter();
            writer.WriteMessage(sent);

            var got = (SnapshotMessage)_client.Read(writer.AsSegment());

            Assert.That(got.Match.Rules.Emote, Is.EqualTo(emote));
            Assert.That(got.Match.Rules.SameAs(sent.Match.Rules), Is.True);
        }
    }
}

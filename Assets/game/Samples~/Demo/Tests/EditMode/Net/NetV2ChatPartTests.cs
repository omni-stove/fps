using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using CoreChatChannel = Shooter.Core.ChatChannel;
using WireChatChannel = Shooter.Protocol.ChatChannel;

namespace Shooter.Net.Tests
{
    public sealed class NetV2ChatPartTests
    {
        private readonly List<ChatRequestMessage> _sent = new List<ChatRequestMessage>();
        private readonly List<string> _logs = new List<string>();
        private NetV2ChatPart _part;
        private int _chatChanges;

        [SetUp]
        public void SetUp()
        {
            GameSignals.Chat.Clear();
            GameSignals.ChatSender.Clear();
            _sent.Clear();
            _logs.Clear();
            _part = new NetV2ChatPart(Send, 3, _logs.Add);
            _chatChanges = 0;
            GameSignals.Chat.Changed += CountChatChange;
        }

        [TearDown]
        public void TearDown()
        {
            GameSignals.Chat.Changed -= CountChatChange;
            _part.Dispose();
            GameSignals.Chat.Clear();
            GameSignals.ChatSender.Clear();
        }

        [Test]
        public void NothingIsPublishedBeforeTheMatchStarts()
        {
            Assert.That(_part.Handle(Message(2, "early")), Is.True);

            Assert.That(GameSignals.Chat.HasValue, Is.False);
            Assert.That(GameSignals.ChatSender.HasValue, Is.False);
        }

        [Test]
        public void TheFirstSnapshotPublishesTheSenderAndTheLinesSoFar()
        {
            _part.Handle(Message(2, "early"));

            _part.Apply(Snapshot(TextChat.On));

            Assert.That(GameSignals.ChatSender.Value, Is.SameAs(_part.ChatSender));
            Assert.That(GameSignals.Chat.Value, Is.EqualTo(new[] { new ChatLine(2, CoreChatChannel.All, "early", 0d) }));
        }

        [Test]
        public void ReceivedLinesArePublishedOldestFirstWithTheirReceiveTime()
        {
            _part.Apply(Snapshot(TextChat.On));
            _part.Handle(Message(2, "one"));
            _part.Tick(1.5f);
            _part.Handle(new ChatMessage { SenderId = 4, Channel = WireChatChannel.Team, Text = "two" });

            Assert.That(GameSignals.Chat.Value, Is.EqualTo(new[]
            {
                new ChatLine(2, CoreChatChannel.All, "one", 0d),
                new ChatLine(4, CoreChatChannel.Team, "two", 1.5d),
            }));
            Assert.That(_chatChanges, Is.EqualTo(3), "once when the match starts and once per line");
        }

        [Test]
        public void AtCapacityTheOldestLineLeavesTheSignal()
        {
            _part.Apply(Snapshot(TextChat.On));
            foreach (var text in new[] { "a", "b", "c", "d" })
            {
                _part.Handle(Message(1, text));
                _part.Tick(1f);
            }

            var lines = GameSignals.Chat.Value;
            Assert.That(lines.Count, Is.EqualTo(3));
            Assert.That(lines[0].Text, Is.EqualTo("b"));
            Assert.That(lines[2].Text, Is.EqualTo("d"));
        }

        [Test]
        public void ThePublishedSenderSendsAChatRequest()
        {
            _part.Apply(Snapshot(TextChat.On));

            GameSignals.ChatSender.Value.Send(CoreChatChannel.Team, " push B ");

            Assert.That(_sent.Count, Is.EqualTo(1));
            Assert.That(_sent[0].Channel, Is.EqualTo(WireChatChannel.Team));
            Assert.That(_sent[0].Text, Is.EqualTo("push B"));
            Assert.That(_logs, Has.Member("[Chat] sent channel=Team length=6"));
        }

        [Test]
        public void WithTextChatOffNeitherTheSenderNorTheLinesArePublished()
        {
            _part.Handle(Message(2, "early"));

            _part.Apply(Snapshot(TextChat.Off));

            Assert.That(GameSignals.ChatSender.HasValue, Is.False);
            Assert.That(GameSignals.Chat.HasValue, Is.False);
            Assert.That(_chatChanges, Is.EqualTo(0));
        }

        [Test]
        public void WithTextChatOffAReceivedLineIsTakenButNotAdded()
        {
            _part.Apply(Snapshot(TextChat.Off));

            Assert.That(_part.Handle(Message(2, "muted")), Is.True, "no other part should get the chat line");

            Assert.That(_part.Chat.Lines, Is.Empty);
            Assert.That(GameSignals.Chat.HasValue, Is.False);
        }

        [Test]
        public void TurningOffMidMatchClearsTheSenderAndTheLines()
        {
            _part.Apply(Snapshot(TextChat.On));
            _part.Handle(Message(2, "one"));
            var sender = GameSignals.ChatSender.Value;

            _part.Apply(Snapshot(TextChat.Off));

            Assert.That(GameSignals.ChatSender.HasValue, Is.False);
            Assert.That(GameSignals.Chat.HasValue, Is.False);
            sender.Send(CoreChatChannel.All, "late");
            Assert.That(_sent, Is.Empty, "a sender kept while off does not send");
            Assert.That(_logs, Has.Member("[Chat] could not send channel=All length=4 (text chat is off)"));
        }

        [Test]
        public void TurningOffDoesNotClearWhatAnotherPartPublished()
        {
            _part.Apply(Snapshot(TextChat.On));
            var otherLines = new[] { new ChatLine(9, CoreChatChannel.All, "other", 0d) };
            GameSignals.Chat.Publish(otherLines);

            _part.Apply(Snapshot(TextChat.Off));

            Assert.That(GameSignals.Chat.Value, Is.SameAs(otherLines));
        }

        [Test]
        public void TurningBackOnPublishesAgainWithTheLinesKeptFromBefore()
        {
            _part.Apply(Snapshot(TextChat.On));
            _part.Handle(Message(2, "before"));
            _part.Apply(Snapshot(TextChat.Off));
            _part.Handle(Message(3, "while off"));

            _part.Apply(Snapshot(TextChat.On));

            Assert.That(GameSignals.ChatSender.Value, Is.SameAs(_part.ChatSender));
            Assert.That(GameSignals.Chat.Value, Is.EqualTo(new[] { new ChatLine(2, CoreChatChannel.All, "before", 0d) }));
            GameSignals.ChatSender.Value.Send(CoreChatChannel.All, "back");
            Assert.That(_sent.Count, Is.EqualTo(1));
        }

        [Test]
        public void RepeatedOnSnapshotsPublishOnlyOnce()
        {
            _part.Apply(Snapshot(TextChat.On));
            _part.Apply(Snapshot(TextChat.On));

            Assert.That(_chatChanges, Is.EqualTo(1));
        }

        [Test]
        public void OtherMessagesAreNotTaken()
        {
            Assert.That(_part.Handle(new PingRemovedMessage()), Is.False);
        }

        [Test]
        public void DisposeClearsOnlyWhatThisPartPublishedAndStopsSending()
        {
            _part.Apply(Snapshot(TextChat.On));
            _part.Handle(Message(1, "a"));
            var sender = GameSignals.ChatSender.Value;

            _part.Dispose();

            Assert.That(GameSignals.Chat.HasValue, Is.False);
            Assert.That(GameSignals.ChatSender.HasValue, Is.False);
            sender.Send(CoreChatChannel.All, "late");
            Assert.That(_sent, Is.Empty, "a sender kept after leaving does not send");
            Assert.That(_part.Handle(Message(1, "late")), Is.False);

            var otherLines = new[] { new ChatLine(9, CoreChatChannel.All, "other", 0d) };
            GameSignals.Chat.Publish(otherLines);
            _part.Dispose();
            Assert.That(GameSignals.Chat.Value, Is.SameAs(otherLines));
        }

        [Test]
        public void TheConstructorRejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2ChatPart(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetV2ChatPart(Send, 0));
        }

        private bool Send(IMessage message)
        {
            var request = (ChatRequestMessage)message;
            _sent.Add(new ChatRequestMessage { Channel = request.Channel, Text = request.Text });
            return true;
        }

        private void CountChatChange(IReadOnlyList<ChatLine> lines)
        {
            _chatChanges++;
        }

        private static SnapshotMessage Snapshot(TextChat textChat)
        {
            var snapshot = new SnapshotMessage();
            snapshot.Match.Rules = new MatchRulesSync { TextChat = textChat };
            return snapshot;
        }

        private static ChatMessage Message(int senderId, string text)
        {
            return new ChatMessage { SenderId = senderId, Channel = WireChatChannel.All, Text = text };
        }
    }
}

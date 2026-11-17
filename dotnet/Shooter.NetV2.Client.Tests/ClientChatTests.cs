using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Core;
using Shooter.Protocol;
using CoreChatChannel = Shooter.Core.ChatChannel;
using WireChatChannel = Shooter.Protocol.ChatChannel;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientChatTests
    {
        private readonly List<(WireChatChannel Channel, string Text)> _sent = new List<(WireChatChannel, string)>();
        private readonly List<string> _logs = new List<string>();
        private static readonly string[] _unknownChannelLogs = { "[Chat] ignored a line with an unknown channel sender=3 channel=7" };
        private static readonly string[] _sentLogs = { "[Chat] sent channel=Team length=10", "[Chat] sent channel=All length=2" };
        private static readonly string[] _notConnectedLogs = { "[Chat] could not send channel=All length=2 (not connected)" };
        private bool _connected;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _connected = true;
        }

        [Test]
        public void ReceivedLinesAreKeptOldestFirst()
        {
            var chat = Chat(4);

            Assert.That(chat.Handle(Message(2, WireChatChannel.All, "first"), 1d), Is.True);
            Assert.That(chat.Handle(Message(5, WireChatChannel.Team, "second"), 2d), Is.True);
            Assert.That(chat.Handle(Message(2, WireChatChannel.All, "third"), 3d), Is.True);

            Assert.That(chat.Lines, Is.EqualTo(new[]
            {
                new ChatLine(2, CoreChatChannel.All, "first", 1d),
                new ChatLine(5, CoreChatChannel.Team, "second", 2d),
                new ChatLine(2, CoreChatChannel.All, "third", 3d),
            }));
            Assert.That(_logs, Has.Member("[Chat] received sender=5 channel=Team length=6 at=2.00"));
            Assert.That(_logs, Has.None.Contains("second"), "the text is not logged");
        }

        [Test]
        public void AtCapacityTheOldestLineIsDropped()
        {
            var chat = Chat(2);

            chat.Handle(Message(1, WireChatChannel.All, "a"), 1d);
            chat.Handle(Message(1, WireChatChannel.All, "b"), 2d);
            chat.Handle(Message(1, WireChatChannel.All, "c"), 3d);

            Assert.That(chat.Lines.Count, Is.EqualTo(2));
            Assert.That(chat.Lines[0].Text, Is.EqualTo("b"));
            Assert.That(chat.Lines[1].Text, Is.EqualTo("c"));
        }

        [Test]
        public void OtherMessagesAreLeftForOtherHandlers()
        {
            var chat = Chat(2);

            Assert.That(chat.Handle(new PingRemovedMessage(), 0d), Is.False);
            Assert.That(chat.Handle(new ChatRequestMessage { Text = "x" }, 0d), Is.False);
            Assert.That(chat.Lines, Is.Empty);
        }

        [Test]
        public void AReceivedMessageNeedsAValidClock()
        {
            var chat = Chat(2);

            Assert.Throws<ArgumentOutOfRangeException>(() => chat.Handle(Message(1, WireChatChannel.All, "a"), -1d));
            Assert.Throws<ArgumentOutOfRangeException>(() => chat.Handle(Message(1, WireChatChannel.All, "a"), double.NaN));
            Assert.That(chat.Lines, Is.Empty);
        }

        [Test]
        public void ALineWithAChannelThisClientDoesNotKnowIsIgnored()
        {
            var chat = Chat(2);

            Assert.That(chat.Handle(Message(3, (WireChatChannel)7, "a"), 0d), Is.True);

            Assert.That(chat.Lines, Is.Empty);
            Assert.That(_logs, Is.EqualTo(_unknownChannelLogs));
        }

        [Test]
        public void SentTextBecomesAChatRequest()
        {
            var chat = Chat(2);

            Assert.That(chat.Send(CoreChatChannel.Team, "  hello team  ", out var refusal), Is.True);
            Assert.That(chat.Send(CoreChatChannel.All, "gg", out _), Is.True);

            Assert.That(refusal, Is.EqualTo(ChatRefusal.None));
            Assert.That(_sent, Is.EqualTo(new[] { (WireChatChannel.Team, "hello team"), (WireChatChannel.All, "gg") }));
            Assert.That(_logs, Is.EqualTo(_sentLogs));
            Assert.That(chat.Lines, Is.Empty, "the line is shown when the server sends it back");
        }

        [Test]
        public void TheSentRequestRoundTripsThroughTheWire()
        {
            IMessage sent = null;
            var chat = new ClientChat(2, message =>
            {
                sent = message;
                return true;
            }, _logs.Add);

            chat.Send(CoreChatChannel.All, "héllo", out _);

            Assert.That(sent.Id, Is.EqualTo(MessageIds.ChatRequest));
            var writer = new MessageWriter();
            sent.Write(writer);
            var read = new ChatRequestMessage();
            read.Read(new MessageReader(writer.ToArray()));
            Assert.That(read.Channel, Is.EqualTo(WireChatChannel.All));
            Assert.That(read.Text, Is.EqualTo("héllo"));
        }

        [Test]
        public void BlankTextIsNotSent()
        {
            var chat = Chat(2);

            Assert.That(chat.Send(CoreChatChannel.All, "   ", out var blank), Is.False);
            Assert.That(chat.Send(CoreChatChannel.All, null, out var none), Is.False);

            Assert.That(blank, Is.EqualTo(ChatRefusal.Empty));
            Assert.That(none, Is.EqualTo(ChatRefusal.Empty));
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Has.Member("[Chat] not sent channel=All reason=Empty"));
        }

        [Test]
        public void TextThatDoesNotFitTheProtocolIsNotSent()
        {
            var chat = Chat(2);

            Assert.That(chat.Send(CoreChatChannel.All, new string('a', MatchProtocol.MaxChatBytes), out var fits), Is.True);
            Assert.That(chat.Send(CoreChatChannel.All, new string('あ', MatchProtocol.MaxChatBytes / 3 + 1), out var tooLong), Is.False);

            Assert.That(fits, Is.EqualTo(ChatRefusal.None));
            Assert.That(tooLong, Is.EqualTo(ChatRefusal.TooLong));
            Assert.That(_sent.Count, Is.EqualTo(1));
            Assert.That(_logs, Has.Member("[Chat] not sent channel=All reason=TooLong"));
        }

        [Test]
        public void ALoneSurrogateIsReplacedInsteadOfBreakingTheWrite()
        {
            var chat = Chat(2);

            Assert.That(chat.Send(CoreChatChannel.All, "a\uD800b", out _), Is.True);

            Assert.That(_sent[0].Text, Is.EqualTo("a�b"));
        }

        [Test]
        public void WhenNotConnectedSendReportsFalse()
        {
            var chat = Chat(2);
            _connected = false;

            Assert.That(chat.Send(CoreChatChannel.All, "hi", out var refusal), Is.False);

            Assert.That(refusal, Is.EqualTo(ChatRefusal.None));
            Assert.That(_logs, Is.EqualTo(_notConnectedLogs));
        }

        [Test]
        public void ClearForgetsTheLines()
        {
            var chat = Chat(2);
            chat.Handle(Message(1, WireChatChannel.All, "a"), 0d);

            chat.Clear();

            Assert.That(chat.Lines, Is.Empty);
        }

        [Test]
        public void TheConstructorAndSendRejectBadArguments()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientChat(0, Send, _logs.Add));
            Assert.Throws<ArgumentNullException>(() => new ClientChat(1, null, _logs.Add));
            Assert.Throws<ArgumentNullException>(() => new ClientChat(1, Send, null));
            Assert.That(new ClientChat(ClientChat.DefaultCapacity, Send, _logs.Add).Capacity, Is.EqualTo(ClientChat.DefaultCapacity));
            Assert.Throws<ArgumentOutOfRangeException>(() => Chat(1).Send((CoreChatChannel)9, "a", out _));
        }

        private ClientChat Chat(int capacity)
        {
            return new ClientChat(capacity, Send, _logs.Add);
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            Assert.That(message, Is.InstanceOf<ChatRequestMessage>());
            var request = (ChatRequestMessage)message;
            _sent.Add((request.Channel, request.Text));
            return true;
        }

        private static ChatMessage Message(int senderId, WireChatChannel channel, string text)
        {
            return new ChatMessage { SenderId = senderId, Channel = channel, Text = text };
        }
    }
}

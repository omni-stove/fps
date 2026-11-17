using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ChatLogTests
    {
        [Test]
        public void FormatsTheGate()
        {
            var gate = new ChatGate(new NetMatchSettings(chatIntervalSeconds: 1f, maxChatLength: 120));

            Assert.That(ChatLog.Gate(gate), Is.EqualTo("[Chat] gate interval=1.00 maxLength=120"));
            Assert.Throws<ArgumentNullException>(() => ChatLog.Gate(null));
        }

        [Test]
        public void FormatsAnAcceptedLineWithoutItsText()
        {
            var line = new ChatLine(1, ChatChannel.Team, "hello", 12.5d);

            Assert.That(ChatLog.Accepted(line), Is.EqualTo("[Chat] accepted sender=1 channel=Team length=5 at=12.50"));
        }

        [Test]
        public void FormatsARefusal()
        {
            Assert.That(ChatLog.Refused(1, ChatRefusal.TooSoon), Is.EqualTo("[Chat] refused sender=1 reason=TooSoon"));
            Assert.That(ChatLog.Refused(3, ChatRefusal.Empty), Is.EqualTo("[Chat] refused sender=3 reason=Empty"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatLog.Refused(1, ChatRefusal.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatLog.Refused(1, (ChatRefusal)99));
        }

        [Test]
        public void FormatsAReceivedLineWithoutItsText()
        {
            var line = new ChatLine(4, ChatChannel.All, "secret", 3.25d);

            Assert.That(ChatLog.Received(line), Is.EqualTo("[Chat] received sender=4 channel=All length=6 at=3.25"));
            Assert.That(ChatLog.IgnoredChannel(4, 9), Is.EqualTo("[Chat] ignored a line with an unknown channel sender=4 channel=9"));
        }

        [Test]
        public void FormatsWhatHappenedToALineThisClientSent()
        {
            Assert.That(ChatLog.Sent(ChatChannel.Team, 7, true), Is.EqualTo("[Chat] sent channel=Team length=7"));
            Assert.That(ChatLog.Sent(ChatChannel.All, 2, false), Is.EqualTo("[Chat] could not send channel=All length=2 (not connected)"));
            Assert.That(ChatLog.NotSent(ChatChannel.All, ChatRefusal.TooLong), Is.EqualTo("[Chat] not sent channel=All reason=TooLong"));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatLog.Sent((ChatChannel)9, 1, true));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatLog.NotSent(ChatChannel.All, ChatRefusal.None));
            Assert.Throws<ArgumentOutOfRangeException>(() => ChatLog.NotSent((ChatChannel)9, ChatRefusal.Empty));
        }
    }
}

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
    }
}

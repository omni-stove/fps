using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class ChatGateTests
    {
        private static ChatGate Gate(float intervalSeconds = 2f, int maxLength = 5)
        {
            return new ChatGate(new NetMatchSettings(chatIntervalSeconds: intervalSeconds, maxChatLength: maxLength));
        }

        [Test]
        public void ReadsTheChatFieldsOfTheSettings()
        {
            var gate = Gate(1.5f, 40);

            Assert.That(gate.IntervalSeconds, Is.EqualTo(1.5f));
            Assert.That(gate.MaxLength, Is.EqualTo(40));
            Assert.That(gate.Count, Is.EqualTo(0));
            Assert.Throws<ArgumentNullException>(() => new ChatGate(null));
        }

        [Test]
        public void AcceptsATrimmedLine()
        {
            var gate = Gate();

            var result = gate.TryAccept(1, ChatChannel.Team, "  hi \t", 3d, out var line);

            Assert.That(result, Is.EqualTo(ChatRefusal.None));
            Assert.That(line, Is.EqualTo(new ChatLine(1, ChatChannel.Team, "hi", 3d)));
            Assert.That(gate.Count, Is.EqualTo(1));
        }

        [Test]
        public void RefusesASecondLineBeforeTheInterval()
        {
            var gate = Gate(intervalSeconds: 2f);
            gate.TryAccept(1, ChatChannel.All, "a", 10d, out _);

            Assert.That(gate.TryAccept(1, ChatChannel.All, "b", 11.99d, out var line), Is.EqualTo(ChatRefusal.TooSoon));
            Assert.That(line, Is.EqualTo(default(ChatLine)));
            Assert.That(gate.TryAccept(1, ChatChannel.All, "c", 12d, out line), Is.EqualTo(ChatRefusal.None));
            Assert.That(line.Text, Is.EqualTo("c"));
        }

        [Test]
        public void RefusedLinesDoNotPushTheIntervalBack()
        {
            var gate = Gate(intervalSeconds: 2f);
            gate.TryAccept(1, ChatChannel.All, "a", 10d, out _);
            gate.TryAccept(1, ChatChannel.All, "b", 11.5d, out _);
            gate.TryAccept(1, ChatChannel.All, "toolong", 12d, out _);

            Assert.That(gate.TryAccept(1, ChatChannel.All, "d", 12d, out _), Is.EqualTo(ChatRefusal.None));
        }

        [Test]
        public void ZeroIntervalAcceptsBackToBack()
        {
            var gate = Gate(intervalSeconds: 0f);

            Assert.That(gate.TryAccept(1, ChatChannel.All, "a", 5d, out _), Is.EqualTo(ChatRefusal.None));
            Assert.That(gate.TryAccept(1, ChatChannel.All, "b", 5d, out _), Is.EqualTo(ChatRefusal.None));
        }

        [Test]
        public void RefusesMoreCharactersThanTheLimitAfterTrimming()
        {
            var gate = Gate(maxLength: 5);

            Assert.That(gate.TryAccept(1, ChatChannel.All, "123456", 0d, out _), Is.EqualTo(ChatRefusal.TooLong));
            Assert.That(gate.TryAccept(1, ChatChannel.All, "  12345  ", 0d, out var line), Is.EqualTo(ChatRefusal.None));
            Assert.That(line.Text, Is.EqualTo("12345"));
        }

        [Test]
        public void RefusesEmptyOrBlankText()
        {
            var gate = Gate();

            Assert.That(gate.TryAccept(1, ChatChannel.All, null, 0d, out _), Is.EqualTo(ChatRefusal.Empty));
            Assert.That(gate.TryAccept(1, ChatChannel.All, string.Empty, 0d, out _), Is.EqualTo(ChatRefusal.Empty));
            Assert.That(gate.TryAccept(1, ChatChannel.All, " \t ", 0d, out _), Is.EqualTo(ChatRefusal.Empty));
            Assert.That(gate.TryAccept(1, ChatChannel.All, "\r\n\n", 0d, out _), Is.EqualTo(ChatRefusal.Empty));
            Assert.That(gate.Count, Is.EqualTo(0));
        }

        [Test]
        public void DropsSurroundingLineBreaksAndFoldsInnerOnesIntoASpace()
        {
            var gate = Gate(intervalSeconds: 0f, maxLength: 10);

            gate.TryAccept(1, ChatChannel.All, "\r\nhi\n", 0d, out var surrounded);
            gate.TryAccept(1, ChatChannel.All, "a\r\nb\nc\rd", 0d, out var inner);

            Assert.That(surrounded.Text, Is.EqualTo("hi"));
            Assert.That(inner.Text, Is.EqualTo("a b c d"));
        }

        [Test]
        public void CountsEachSenderApart()
        {
            var gate = Gate(intervalSeconds: 2f);
            gate.TryAccept(1, ChatChannel.All, "a", 10d, out _);

            Assert.That(gate.TryAccept(2, ChatChannel.All, "b", 10.5d, out _), Is.EqualTo(ChatRefusal.None));
            Assert.That(gate.TryAccept(1, ChatChannel.All, "c", 11d, out _), Is.EqualTo(ChatRefusal.TooSoon));
            Assert.That(gate.TryAccept(2, ChatChannel.All, "d", 12d, out _), Is.EqualTo(ChatRefusal.TooSoon));
            Assert.That(gate.TryAccept(1, ChatChannel.All, "e", 12d, out _), Is.EqualTo(ChatRefusal.None));
            Assert.That(gate.TryAccept(2, ChatChannel.All, "f", 12.5d, out _), Is.EqualTo(ChatRefusal.None));
            Assert.That(gate.Count, Is.EqualTo(2));
        }

        [Test]
        public void RemoveAndClearForgetThePreviousLine()
        {
            var gate = Gate(intervalSeconds: 2f);
            gate.TryAccept(1, ChatChannel.All, "a", 10d, out _);
            gate.TryAccept(2, ChatChannel.All, "b", 10d, out _);

            Assert.That(gate.Remove(1), Is.True);
            Assert.That(gate.Remove(1), Is.False);
            Assert.That(gate.TryAccept(1, ChatChannel.All, "c", 10.5d, out _), Is.EqualTo(ChatRefusal.None));

            gate.Clear();
            Assert.That(gate.Count, Is.EqualTo(0));
            Assert.That(gate.TryAccept(2, ChatChannel.All, "d", 0d, out _), Is.EqualTo(ChatRefusal.None));
        }

        [Test]
        public void RejectsABadChannelOrTime()
        {
            var gate = Gate();

            Assert.Throws<ArgumentOutOfRangeException>(() => gate.TryAccept(1, (ChatChannel)99, "a", 0d, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.TryAccept(1, ChatChannel.All, "a", -1d, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.TryAccept(1, ChatChannel.All, "a", double.NaN, out _));
            Assert.Throws<ArgumentOutOfRangeException>(() => gate.TryAccept(1, ChatChannel.All, "a", double.PositiveInfinity, out _));
            Assert.That(gate.Count, Is.EqualTo(0));
        }
    }
}

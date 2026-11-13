using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class LobbyAttributeTests
    {
        [Test]
        public void TextAndNumberAttributes()
        {
            var text = LobbyAttribute.OfText("server", "203.0.113.10:7777");
            var number = LobbyAttribute.OfNumber("players", 3);

            Assert.That(text.IsText, Is.True);
            Assert.That(text.ToString(), Is.EqualTo("server=203.0.113.10:7777"));
            Assert.That(number.IsText, Is.False);
            Assert.That(number.Text, Is.Null);
            Assert.That(number.ToString(), Is.EqualTo("players=3"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Players")]
        [TestCase("max players")]
        [TestCase("players-count")]
        public void RejectsInvalidKeys(string key)
        {
            Assert.Throws<ArgumentException>(() => LobbyAttribute.OfNumber(key, 0));
            Assert.Throws<ArgumentException>(() => LobbyAttribute.OfText(key, "x"));
        }

        [Test]
        public void KeyLengthIsLimited()
        {
            Assert.That(LobbyAttribute.OfNumber(new string('a', LobbyAttribute.MaxKeyLength), 0).Key.Length,
                Is.EqualTo(LobbyAttribute.MaxKeyLength));
            Assert.Throws<ArgumentException>(() => LobbyAttribute.OfNumber(new string('a', LobbyAttribute.MaxKeyLength + 1), 0));
        }

        [Test]
        public void RejectsInvalidTextValues()
        {
            Assert.Throws<ArgumentException>(() => LobbyAttribute.OfText("server", null));
            Assert.Throws<ArgumentException>(() => LobbyAttribute.OfText("server", ""));
            Assert.Throws<ArgumentException>(() => LobbyAttribute.OfText("server", new string('a', LobbyAttribute.MaxTextLength + 1)));
            Assert.That(LobbyAttribute.OfText("server", new string('a', LobbyAttribute.MaxTextLength)).Text.Length,
                Is.EqualTo(LobbyAttribute.MaxTextLength));
        }
    }
}

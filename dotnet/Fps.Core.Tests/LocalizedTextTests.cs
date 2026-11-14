using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class LocalizedTextTests
    {
        [Test]
        public void KeepsTheKeyAndArgumentsInOrder()
        {
            var nested = new LocalizedText("team.alpha");
            var text = new LocalizedText("match.win", 3, 1.5f, "Bob", nested);

            Assert.That(text.Key, Is.EqualTo("match.win"));
            Assert.That(text.Args, Is.EqualTo(new object[] { 3, 1.5f, "Bob", nested }));
            Assert.That(text.IsLiteral, Is.False);
            Assert.That(text.IsEmpty, Is.False);
        }

        [Test]
        public void CopiesTheArgumentsSoLaterChangesToTheArrayDoNotLeakIn()
        {
            var args = new object[] { 1 };
            var text = new LocalizedText("score", args);

            args[0] = 2;

            Assert.That(text.Args[0], Is.EqualTo(1));
        }

        [Test]
        public void HasNoArgumentsWhenNoneAreGiven()
        {
            Assert.That(new LocalizedText("hello").Args, Is.Empty);
            Assert.That(new LocalizedText("hello", null).Args, Is.Empty);
        }

        [Test]
        public void RejectsANullKey()
        {
            var ex = Assert.Throws<ArgumentNullException>(() => new LocalizedText(null));
            Assert.That(ex.ParamName, Is.EqualTo("key"));
        }

        [Test]
        public void RejectsAnEmptyKey()
        {
            var ex = Assert.Throws<ArgumentException>(() => new LocalizedText(string.Empty));
            Assert.That(ex.ParamName, Is.EqualTo("key"));
        }

        [Test]
        public void RejectsArgumentsThatAreNotIntFloatStringOrLocalizedText()
        {
            Assert.Throws<ArgumentException>(() => new LocalizedText("k", 1.5d));
            Assert.Throws<ArgumentException>(() => new LocalizedText("k", 1L));
            Assert.Throws<ArgumentException>(() => new LocalizedText("k", new object()));
            Assert.Throws<ArgumentException>(() => new LocalizedText("k", "ok", null));
        }

        [Test]
        public void LiteralCarriesTheTextAsIs()
        {
            var text = LocalizedText.Literal("GG {0}");

            Assert.That(text.IsLiteral, Is.True);
            Assert.That(text.Key, Is.EqualTo("GG {0}"));
            Assert.That(text.Args, Is.Empty);
        }

        [Test]
        public void LiteralAcceptsAnEmptyStringButNotNull()
        {
            Assert.That(LocalizedText.Literal(string.Empty).Key, Is.Empty);
            Assert.Throws<ArgumentNullException>(() => LocalizedText.Literal(null));
        }

        [Test]
        public void DefaultIsEmpty()
        {
            var text = default(LocalizedText);

            Assert.That(text.IsEmpty, Is.True);
            Assert.That(text.Key, Is.Empty);
            Assert.That(text.Args, Is.Empty);
        }

        [Test]
        public void EqualsComparesKeyArgumentsAndLiteralness()
        {
            var a = new LocalizedText("win", 1, new LocalizedText("team", "A"));
            var b = new LocalizedText("win", 1, new LocalizedText("team", "A"));

            Assert.That(a == b, Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a != new LocalizedText("win", 2, new LocalizedText("team", "A")), Is.True);
            Assert.That(a != new LocalizedText("win", 1), Is.True);
            Assert.That(new LocalizedText("win") != LocalizedText.Literal("win"), Is.True);
            Assert.That(new LocalizedText("win", 1) != new LocalizedText("win", 1f), Is.True);
        }

        [Test]
        public void ToStringShowsTheKeyAndArgumentsForLogs()
        {
            Assert.That(new LocalizedText("win", "Alpha", 2.5f).ToString(), Is.EqualTo("win(Alpha, 2.5)"));
            Assert.That(new LocalizedText("win").ToString(), Is.EqualTo("win"));
            Assert.That(LocalizedText.Literal("GG").ToString(), Is.EqualTo("\"GG\""));
        }
    }
}

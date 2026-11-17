using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class JoinCodeTests
    {
        [Test]
        public void TheAlphabetLeavesOutLookAlikes()
        {
            Assert.That(JoinCode.Alphabet, Is.EqualTo("23456789ABCDEFGHJKMNPQRSTUVWXYZ"));
            foreach (var c in "01OIL")
            {
                Assert.That(JoinCode.Alphabet.IndexOf(c), Is.EqualTo(-1), c.ToString());
            }
        }

        [Test]
        public void GeneratesTheGivenLengthFromTheAlphabet()
        {
            var next = 0;
            var code = JoinCode.Generate(6, count => next++ % count);

            Assert.That(code, Is.EqualTo("234567"));
        }

        [Test]
        public void GeneratesTheLastLetterAtTheLastIndex()
        {
            Assert.That(JoinCode.Generate(3, count => count - 1), Is.EqualTo("ZZZ"));
            Assert.That(JoinCode.Generate(1, count => 0), Is.EqualTo("2"));
        }

        [Test]
        public void AsksForAnIndexBelowTheAlphabetLength()
        {
            var asked = 0;
            JoinCode.Generate(4, count =>
            {
                asked = count;
                return 0;
            });

            Assert.That(asked, Is.EqualTo(31));
        }

        [Test]
        public void RandomCodesHaveTheLengthAndOnlyAlphabetLetters()
        {
            for (var i = 0; i < 50; i++)
            {
                var code = JoinCode.Generate(JoinCode.DefaultLength);

                Assert.That(code.Length, Is.EqualTo(6));
                foreach (var c in code)
                {
                    Assert.That(JoinCode.Alphabet.IndexOf(c), Is.GreaterThanOrEqualTo(0), code);
                }

                Assert.That(JoinCode.TryNormalize(code, out var normalized), Is.True);
                Assert.That(normalized, Is.EqualTo(code));
            }
        }

        [Test]
        public void RandomCodesFitTheConnectionPayload()
        {
            var code = JoinCode.Generate(JoinCode.MaxLength);

            Assert.That(code.Length, Is.EqualTo(16));
            Assert.That(new ConnectionExtras(null, code, null, null).JoinCode, Is.EqualTo(code));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(17)]
        public void RejectsALengthOutOfRange(int length)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JoinCode.Generate(length));
            Assert.Throws<ArgumentOutOfRangeException>(() => JoinCode.Generate(length, count => 0));
        }

        [TestCase(-1)]
        [TestCase(31)]
        public void RejectsAnIndexOutOfTheAlphabet(int index)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => JoinCode.Generate(4, count => index));
        }

        [Test]
        public void RejectsANullIndexSource()
        {
            Assert.Throws<ArgumentNullException>(() => JoinCode.Generate(4, null));
        }

        [TestCase("abc123", "ABC123")]
        [TestCase(" ab c\t12 3\n", "ABC123")]
        [TestCase("Room_42", "ROOM_42")]
        [TestCase("abc-123", "ABC-123")]
        [TestCase("XYZ789", "XYZ789")]
        public void NormalizesCaseAndWhitespace(string text, string expected)
        {
            Assert.That(JoinCode.TryNormalize(text, out var code), Is.True);
            Assert.That(code, Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("ab/12")]
        [TestCase("ab.12")]
        [TestCase("été")]
        [TestCase("ABCDEFGHJKMNPQRST")]
        public void CannotNormalizeBlankInvalidOrTooLongText(string text)
        {
            Assert.That(JoinCode.TryNormalize(text, out var code), Is.False);
            Assert.That(code, Is.Null);
        }

        [Test]
        public void SpacesDoNotCountTowardTheLength()
        {
            Assert.That(JoinCode.TryNormalize("ABCD EFGH JKMN PQRS", out var code), Is.True);
            Assert.That(code, Is.EqualTo("ABCDEFGHJKMNPQRS"));
        }

        [TestCase("ABC123", "abc123", true)]
        [TestCase("ABC123", " abc 123 ", true)]
        [TestCase("room_42", "ROOM_42", true)]
        [TestCase("ABC123", "ABC124", false)]
        [TestCase("ABC123", "ABC12", false)]
        [TestCase("ABC123", "ABC-123", false)]
        [TestCase("ABC123", null, false)]
        [TestCase("ABC123", "", false)]
        [TestCase(null, null, false)]
        [TestCase(null, "ABC123", false)]
        [TestCase("", "", false)]
        public void MatchesAfterNormalizingBoth(string expected, string entered, bool matches)
        {
            Assert.That(JoinCode.Matches(expected, entered), Is.EqualTo(matches));
        }

        [Test]
        public void LookAlikesAreNotFoldedTogether()
        {
            Assert.That(JoinCode.Matches("ABC0", "ABCO"), Is.False);
            Assert.That(JoinCode.Matches("ABC1", "ABCI"), Is.False);
        }
    }
}

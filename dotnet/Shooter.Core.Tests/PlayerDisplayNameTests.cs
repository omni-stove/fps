using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    public sealed class PlayerDisplayNameTests
    {
        private const int _max = NetMatchSettings.DefaultMaxDisplayNameLength;

        // An attribute argument must be a constant, and a raw U+2028 in the source would end the line.
        private static readonly string[] SeparatorAndBidiNames =
        {
            "A" + (char)0x2028 + "ce",
            "A" + (char)0x2029 + "ce",
            "A" + (char)0x202E + "ce",
            "A" + (char)0x2066 + "ce",
        };

        [TestCase("Ace", "Ace")]
        [TestCase("  Ace  ", "Ace")]
        [TestCase("　名無し　", "名無し")]
        [TestCase("Ace of Spades", "Ace of Spades")]
        [TestCase("😀 smile", "😀 smile")]
        public void AGoodNameIsTrimmedAndKept(string name, string expected)
        {
            Assert.That(PlayerDisplayName.Sanitize(name, _max), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\t")]
        public void ANameWithNothingToShowIsEmpty(string name)
        {
            Assert.That(PlayerDisplayName.Sanitize(name, _max), Is.Empty);
        }

        [TestCase("A\nce")]
        [TestCase("A\rce")]
        [TestCase("A\tce")]
        [TestCase("A\u0000ce")]
        [TestCase("A\u001Bce")]
        [TestCase("A\u007Fce")]
        [TestCase("A\u0085ce")]
        [TestCaseSource(nameof(SeparatorAndBidiNames))]
        public void ANameWithAControlCharacterIsEmpty(string name)
        {
            Assert.That(PlayerDisplayName.Sanitize(name, _max), Is.Empty);
        }

        [Test]
        public void ANameWithALoneSurrogateIsEmpty()
        {
            Assert.That(PlayerDisplayName.Sanitize("A\uD83Dce", _max), Is.Empty);
        }

        [Test]
        public void ANameOfTheLimitIsKeptAndOneMoreIsEmpty()
        {
            Assert.That(PlayerDisplayName.Sanitize(new string('a', 16), 16), Is.EqualTo(new string('a', 16)));
            Assert.That(PlayerDisplayName.Sanitize(new string('a', 17), 16), Is.Empty);
            Assert.That(PlayerDisplayName.Sanitize("  " + new string('a', 16) + "  ", 16), Is.EqualTo(new string('a', 16)), "the spaces trimmed away do not count");
        }

        [Test]
        public void TheLimitComesFromTheCaller()
        {
            Assert.That(PlayerDisplayName.Sanitize("Ace", 2), Is.Empty);
            Assert.That(PlayerDisplayName.Sanitize("Ace", 3), Is.EqualTo("Ace"));
        }

        [Test]
        public void TheLongestNameOfAnyLimitFitsTheBytesOfTheConnectRequest()
        {
            var longest = new string('あ', NetMatchSettings.MaxMaxDisplayNameLength);

            Assert.That(NetMatchSettings.MaxMaxDisplayNameLength, Is.EqualTo(21));
            Assert.That(PlayerDisplayName.MaxBytes, Is.EqualTo(64));
            Assert.That(PlayerDisplayName.Sanitize(longest, NetMatchSettings.MaxMaxDisplayNameLength), Is.EqualTo(longest));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(NetMatchSettings.MaxMaxDisplayNameLength + 1)]
        public void ALimitOutsideTheSettingsIsRefused(int maxLength)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerDisplayName.Sanitize("Ace", maxLength));
            Assert.Throws<ArgumentOutOfRangeException>(() => PlayerDisplayName.IsValid("Ace", maxLength));
        }

        [TestCase("Ace", true)]
        [TestCase("", false)]
        [TestCase(null, false)]
        [TestCase(" Ace", false)]
        [TestCase("Ace ", false)]
        [TestCase("A\nce", false)]
        public void IsValidOnlyAcceptsWhatSanitizeReturns(string name, bool expected)
        {
            Assert.That(PlayerDisplayName.IsValid(name, _max), Is.EqualTo(expected));
        }
    }
}

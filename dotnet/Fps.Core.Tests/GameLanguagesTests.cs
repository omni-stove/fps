using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class GameLanguagesTests
    {
        [Test]
        public void ListsJapaneseThenEnglish()
        {
            Assert.That(GameLanguages.All, Is.EqualTo(new[] { GameLanguage.Japanese, GameLanguage.English }));
        }

        [TestCase(GameLanguage.Japanese, "ja", "日本語")]
        [TestCase(GameLanguage.English, "en", "English")]
        public void CodesAndNamesAreFixed(GameLanguage language, string code, string nativeName)
        {
            Assert.That(GameLanguages.Code(language), Is.EqualTo(code));
            Assert.That(GameLanguages.NativeName(language), Is.EqualTo(nativeName));
            Assert.That(GameLanguages.TryParseCode(code, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(language));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("JA")]
        [TestCase("ja-JP")]
        [TestCase("fr")]
        public void UnknownCodesAreRejected(string code)
        {
            Assert.That(GameLanguages.TryParseCode(code, out _), Is.False);
        }

        [Test]
        public void NextCyclesThroughEveryLanguage()
        {
            Assert.That(GameLanguages.Next(GameLanguage.Japanese), Is.EqualTo(GameLanguage.English));
            Assert.That(GameLanguages.Next(GameLanguage.English), Is.EqualTo(GameLanguage.Japanese));
        }

        [Test]
        public void RejectsUnknownLanguages()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GameLanguages.Code((GameLanguage)2));
            Assert.Throws<ArgumentOutOfRangeException>(() => GameLanguages.NativeName((GameLanguage)2));
        }
    }
}

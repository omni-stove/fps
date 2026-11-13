using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class CampaignLaunchOptionsTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static CampaignLaunchOptions ParseOrFail(string commandLine)
        {
            var ok = CampaignLaunchOptions.TryParse(Args(commandLine), out var options, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return options;
        }

        private static string ParseError(string commandLine)
        {
            var ok = CampaignLaunchOptions.TryParse(Args(commandLine), out var options, out var error);
            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [Test]
        public void NoDifficultyMeansNone()
        {
            var options = ParseOrFail("/path/to/fps -batchmode -nographics -mode host");

            Assert.That(options, Is.SameAs(CampaignLaunchOptions.None));
            Assert.That(options.Difficulty, Is.Null);
        }

        [Test]
        public void EmptyArgumentsMeanNone()
        {
            var ok = CampaignLaunchOptions.TryParse(Array.Empty<string>(), out var options, out var error);

            Assert.That(ok, Is.True);
            Assert.That(error, Is.Null);
            Assert.That(options.Difficulty, Is.Null);
        }

        [TestCase("-difficulty easy", CampaignDifficulty.Easy)]
        [TestCase("-difficulty normal", CampaignDifficulty.Normal)]
        [TestCase("-difficulty hard", CampaignDifficulty.Hard)]
        [TestCase("-DIFFICULTY Hard", CampaignDifficulty.Hard)]
        [TestCase("/path/to/fps -batchmode -difficulty EASY -logFile -", CampaignDifficulty.Easy)]
        public void ReadsTheDifficulty(string commandLine, CampaignDifficulty expected)
        {
            var options = ParseOrFail(commandLine);

            Assert.That(options.Difficulty, Is.EqualTo(expected));
        }

        [Test]
        public void RejectsAMissingValue()
        {
            Assert.That(ParseError("-difficulty"), Does.Contain(CampaignLaunchOptions.DifficultyFlag));
            Assert.That(ParseError("-difficulty -batchmode"), Does.Contain("requires a value"));
        }

        [Test]
        public void RejectsAnUnknownDifficulty()
        {
            Assert.That(ParseError("-difficulty nightmare"), Does.Contain("'nightmare'"));
            Assert.That(ParseError("-difficulty 1"), Does.Contain("easy, normal or hard"));
        }

        [Test]
        public void RejectsTheFlagGivenTwice()
        {
            Assert.That(ParseError("-difficulty easy -difficulty hard"), Does.Contain("more than once"));
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(() => CampaignLaunchOptions.TryParse(null, out _, out _));
        }

        [TestCase(CampaignDifficulty.Easy, "easy")]
        [TestCase(CampaignDifficulty.Normal, "normal")]
        [TestCase(CampaignDifficulty.Hard, "hard")]
        public void NamesRoundTrip(CampaignDifficulty difficulty, string name)
        {
            Assert.That(CampaignLaunchOptions.DifficultyName(difficulty), Is.EqualTo(name));
            Assert.That(CampaignLaunchOptions.TryParseDifficulty(name, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(difficulty));
            Assert.That(CampaignLaunchOptions.WithDifficulty(difficulty).ToString(), Is.EqualTo(name));
        }

        [Test]
        public void RejectsUndefinedDifficultyValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignLaunchOptions.DifficultyName((CampaignDifficulty)99));
            Assert.Throws<ArgumentOutOfRangeException>(() => CampaignLaunchOptions.WithDifficulty((CampaignDifficulty)99));
            Assert.That(CampaignLaunchOptions.TryParseDifficulty(null, out _), Is.False);
        }
    }
}

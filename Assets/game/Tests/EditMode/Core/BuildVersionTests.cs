using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class BuildVersionTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static string NormalizeOrFail(string text)
        {
            var ok = BuildVersion.TryNormalize(text, out var version, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return version;
        }

        private static string NormalizeError(string text)
        {
            var ok = BuildVersion.TryNormalize(text, out var version, out var error);
            Assert.That(ok, Is.False);
            Assert.That(version, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [TestCase("v0.2.0", "0.2.0")]
        [TestCase("V1.0", "1.0")]
        [TestCase("0.2.0", "0.2.0")]
        [TestCase("v0.2.0-3-gabc1234-dirty", "0.2.0-3-gabc1234-dirty")]
        [TestCase("v1.0.0-rc.1_build-7", "1.0.0-rc.1_build-7")]
        [TestCase("abc1234", "abc1234")]
        [TestCase("abc1234-dirty", "abc1234-dirty")]
        public void StripsLeadingV(string text, string expected)
        {
            Assert.That(NormalizeOrFail(text), Is.EqualTo(expected));
        }

        [Test]
        public void TrimsGitOutputNewline()
        {
            Assert.That(NormalizeOrFail("  v0.2.0\n"), Is.EqualTo("0.2.0"));
            Assert.That(NormalizeOrFail("abc1234\r\n"), Is.EqualTo("abc1234"));
        }

        [TestCase("vnext", "vnext")]
        [TestCase("v", "v")]
        public void KeepsVNotFollowedByDigit(string text, string expected)
        {
            Assert.That(NormalizeOrFail(text), Is.EqualTo(expected));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("\n")]
        public void RejectsEmpty(string text)
        {
            Assert.That(NormalizeError(text), Does.Contain("empty"));
        }

        [Test]
        public void AcceptsMaxLengthAndRejectsLonger()
        {
            var max = new string('1', BuildVersion.MaxLength);
            Assert.That(NormalizeOrFail(max), Is.EqualTo(max));
            Assert.That(NormalizeOrFail("v" + max), Is.EqualTo(max));
            Assert.That(NormalizeError(max + "1"), Does.Contain(BuildVersion.MaxLength.ToString()));
        }

        [TestCase("0.2.0 beta")]
        [TestCase("0.2.0\nfoo")]
        [TestCase("0.2/0")]
        [TestCase("0.2.0\"")]
        [TestCase("1.0.0+build.7")]
        [TestCase("veré")]
        public void RejectsDisallowedCharacters(string text)
        {
            Assert.That(NormalizeError(text), Does.Contain("contains"));
        }

        [TestCase(".1")]
        [TestCase("-1")]
        [TestCase(".rc")]
        public void RejectsLeadingDotOrDash(string text)
        {
            Assert.That(NormalizeError(text), Does.Contain("starts with"));
        }

        [Test]
        public void AcceptsLeadingUnderscore()
        {
            Assert.That(NormalizeOrFail("_wip"), Is.EqualTo("_wip"));
        }

        [Test]
        public void NormalizeThrowsOnInvalid()
        {
            Assert.That(BuildVersion.Normalize("v0.2.0"), Is.EqualTo("0.2.0"));
            Assert.Throws<ArgumentException>(() => BuildVersion.Normalize(""));
        }

        [Test]
        public void MaxLengthFitsInLobbyBucket()
        {
            Assert.That(BuildVersion.MaxLength, Is.LessThanOrEqualTo(LobbyLaunchOptions.MaxBucketVersionLength));
        }

        [Test]
        public void MissingFlagGivesNoVersion()
        {
            var ok = BuildVersion.TryParseArgs(Args("Unity -batchmode -quit -serverPlatform linux"), out var version, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(version, Is.Null);
            Assert.That(error, Is.Null);
        }

        [Test]
        public void ParsesAndNormalizesFlag()
        {
            var ok = BuildVersion.TryParseArgs(
                Args("-batchmode -executeMethod Fps.Editor.ServerBuilder.Build -serverPlatform linux -buildVersion v0.2.0"),
                out var version,
                out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(version, Is.EqualTo("0.2.0"));
        }

        [Test]
        public void FlagIsCaseInsensitive()
        {
            var ok = BuildVersion.TryParseArgs(Args("-BUILDVERSION 1.2"), out var version, out var error);

            Assert.That(ok, Is.True, error);
            Assert.That(version, Is.EqualTo("1.2"));
        }

        [TestCase("-buildVersion")]
        [TestCase("-buildVersion -quit")]
        public void RejectsMissingValue(string commandLine)
        {
            var ok = BuildVersion.TryParseArgs(Args(commandLine), out var version, out var error);

            Assert.That(ok, Is.False);
            Assert.That(version, Is.Null);
            Assert.That(error, Does.Contain("requires a value"));
        }

        [Test]
        public void RejectsBlankValue()
        {
            var ok = BuildVersion.TryParseArgs(new[] { "-buildVersion", " " }, out var version, out var error);

            Assert.That(ok, Is.False);
            Assert.That(version, Is.Null);
            Assert.That(error, Does.Contain("requires a value"));
        }

        [Test]
        public void RejectsRepeatedFlag()
        {
            var ok = BuildVersion.TryParseArgs(Args("-buildVersion 1.0 -buildVersion 2.0"), out var version, out var error);

            Assert.That(ok, Is.False);
            Assert.That(version, Is.Null);
            Assert.That(error, Does.Contain("more than once"));
        }

        [Test]
        public void RejectsInvalidValue()
        {
            var ok = BuildVersion.TryParseArgs(new[] { "-buildVersion", new string('1', BuildVersion.MaxLength + 1) }, out var version, out var error);

            Assert.That(ok, Is.False);
            Assert.That(version, Is.Null);
            Assert.That(error, Does.StartWith(BuildVersion.Flag));
        }

        [Test]
        public void RejectsNullArgs()
        {
            Assert.Throws<ArgumentNullException>(() => BuildVersion.TryParseArgs(null, out _, out _));
        }

        [Test]
        public void FormatsLogLine()
        {
            Assert.That(BuildVersion.LogLine("0.2.0"), Is.EqualTo("[Version] 0.2.0"));
            Assert.That(BuildVersion.LogLine(" abc1234 "), Is.EqualTo("[Version] abc1234"));
            Assert.That(BuildVersion.LogLine(""), Is.EqualTo("[Version] unknown"));
            Assert.That(BuildVersion.LogLine(null), Is.EqualTo("[Version] unknown"));
        }
    }
}

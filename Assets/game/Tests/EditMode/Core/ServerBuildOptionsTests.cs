using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ServerBuildOptionsTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static ServerBuildOptions ParseOrFail(string commandLine, ServerPlatform defaultPlatform = ServerPlatform.MacOS)
        {
            var ok = ServerBuildOptions.TryParse(Args(commandLine), defaultPlatform, out var options, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return options;
        }

        private static string ParseError(string commandLine)
        {
            var ok = ServerBuildOptions.TryParse(Args(commandLine), ServerPlatform.MacOS, out var options, out var error);
            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [Test]
        public void NoFlagUsesDefault()
        {
            Assert.That(ParseOrFail("Unity -batchmode -quit").Platform, Is.EqualTo(ServerPlatform.MacOS));
            Assert.That(ParseOrFail("", ServerPlatform.Linux).Platform, Is.EqualTo(ServerPlatform.Linux));
        }

        [TestCase("macos", ServerPlatform.MacOS)]
        [TestCase("linux", ServerPlatform.Linux)]
        [TestCase("Linux", ServerPlatform.Linux)]
        public void ParsesPlatform(string value, ServerPlatform expected)
        {
            var options = ParseOrFail("-batchmode -executeMethod Fps.Editor.ServerBuilder.Build -serverPlatform " + value);

            Assert.That(options.Platform, Is.EqualTo(expected));
        }

        [Test]
        public void FlagIsCaseInsensitive()
        {
            Assert.That(ParseOrFail("-SERVERPLATFORM linux").Platform, Is.EqualTo(ServerPlatform.Linux));
        }

        [TestCase("-serverPlatform")]
        [TestCase("-serverPlatform -quit")]
        public void RejectsMissingValue(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("requires a value"));
        }

        [TestCase("-serverPlatform windows")]
        [TestCase("-serverPlatform osx")]
        public void RejectsUnknownPlatform(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("macos or linux"));
        }

        [Test]
        public void RejectsDuplicateFlag()
        {
            Assert.That(ParseError("-serverPlatform macos -serverPlatform linux"), Does.Contain("more than once"));
        }

        [Test]
        public void OutputGoesUnderBuildsPerPlatform()
        {
            var mac = new ServerBuildOptions(ServerPlatform.MacOS);
            var linux = new ServerBuildOptions(ServerPlatform.Linux);

            Assert.That(mac.OutputDirectory, Is.EqualTo("Builds/server-macos"));
            Assert.That(mac.ExecutablePath, Is.EqualTo("Builds/server-macos/fps-server"));
            Assert.That(linux.OutputDirectory, Is.EqualTo("Builds/server-linux"));
            Assert.That(linux.ExecutablePath, Is.EqualTo("Builds/server-linux/fps-server.x86_64"));
        }

        [Test]
        public void RejectsUnknownEnumValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ServerBuildOptions((ServerPlatform)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ServerBuildOptions.TryParse(Args(""), (ServerPlatform)99, out _, out _));
            Assert.Throws<ArgumentNullException>(
                () => ServerBuildOptions.TryParse(null, ServerPlatform.MacOS, out _, out _));
        }

        [Test]
        public void FormatsForLogs()
        {
            Assert.That(new ServerBuildOptions(ServerPlatform.Linux).ToString(), Is.EqualTo("linux -> Builds/server-linux/fps-server.x86_64"));
        }
    }
}

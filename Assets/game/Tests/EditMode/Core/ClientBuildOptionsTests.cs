using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class ClientBuildOptionsTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static ClientBuildOptions ParseOrFail(string commandLine, ClientPlatform defaultPlatform = ClientPlatform.MacOS)
        {
            var ok = ClientBuildOptions.TryParse(Args(commandLine), defaultPlatform, out var options, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return options;
        }

        private static string ParseError(string commandLine)
        {
            var ok = ClientBuildOptions.TryParse(Args(commandLine), ClientPlatform.MacOS, out var options, out var error);
            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [Test]
        public void NoFlagUsesDefault()
        {
            Assert.That(ParseOrFail("Unity -batchmode -quit").Platform, Is.EqualTo(ClientPlatform.MacOS));
            Assert.That(ParseOrFail("", ClientPlatform.Windows).Platform, Is.EqualTo(ClientPlatform.Windows));
        }

        [TestCase("windows", ClientPlatform.Windows)]
        [TestCase("macos", ClientPlatform.MacOS)]
        [TestCase("Windows", ClientPlatform.Windows)]
        public void ParsesPlatform(string value, ClientPlatform expected)
        {
            var options = ParseOrFail("-batchmode -executeMethod Fps.Editor.ClientBuilder.Build -clientPlatform " + value);

            Assert.That(options.Platform, Is.EqualTo(expected));
        }

        [Test]
        public void FlagIsCaseInsensitive()
        {
            Assert.That(ParseOrFail("-CLIENTPLATFORM windows").Platform, Is.EqualTo(ClientPlatform.Windows));
        }

        [Test]
        public void IgnoresTheServerFlag()
        {
            Assert.That(ParseOrFail("-serverPlatform linux", ClientPlatform.Windows).Platform, Is.EqualTo(ClientPlatform.Windows));
        }

        [TestCase("-clientPlatform")]
        [TestCase("-clientPlatform -quit")]
        public void RejectsMissingValue(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("requires a value"));
        }

        [TestCase("-clientPlatform linux")]
        [TestCase("-clientPlatform win64")]
        [TestCase("-clientPlatform osx")]
        public void RejectsUnknownPlatform(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("windows or macos"));
        }

        [Test]
        public void RejectsDuplicateFlag()
        {
            Assert.That(ParseError("-clientPlatform macos -clientPlatform windows"), Does.Contain("more than once"));
        }

        [Test]
        public void OutputGoesUnderBuildsPerPlatform()
        {
            var windows = new ClientBuildOptions(ClientPlatform.Windows);
            var mac = new ClientBuildOptions(ClientPlatform.MacOS);

            Assert.That(windows.OutputDirectory, Is.EqualTo("Builds/client-windows"));
            Assert.That(windows.ExecutablePath, Is.EqualTo("Builds/client-windows/fps.exe"));
            Assert.That(mac.OutputDirectory, Is.EqualTo("Builds/client-macos"));
            Assert.That(mac.ExecutablePath, Is.EqualTo("Builds/client-macos/fps.app"));
        }

        [Test]
        public void RejectsUnknownEnumValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ClientBuildOptions((ClientPlatform)99));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => ClientBuildOptions.TryParse(Args(""), (ClientPlatform)99, out _, out _));
            Assert.Throws<ArgumentNullException>(
                () => ClientBuildOptions.TryParse(null, ClientPlatform.MacOS, out _, out _));
        }

        [Test]
        public void FormatsForLogs()
        {
            Assert.That(new ClientBuildOptions(ClientPlatform.Windows).ToString(), Is.EqualTo("windows -> Builds/client-windows/fps.exe"));
        }
    }
}

using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetLaunchOptionsTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static NetLaunchOptions ParseOrFail(string commandLine, NetLaunchMode defaultMode = NetLaunchMode.Host)
        {
            var ok = NetLaunchOptions.TryParse(Args(commandLine), defaultMode, out var options, out var error);
            Assert.That(ok, Is.True, error);
            Assert.That(error, Is.Null);
            return options;
        }

        private static string ParseError(string commandLine)
        {
            var ok = NetLaunchOptions.TryParse(Args(commandLine), NetLaunchMode.Host, out var options, out var error);
            Assert.That(ok, Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Is.Not.Null.And.Not.Empty);
            return error;
        }

        [Test]
        public void NoArgumentsUseDefaults()
        {
            var options = ParseOrFail("/path/to/fps -batchmode -nographics");

            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Host));
            Assert.That(options.Address, Is.EqualTo(NetLaunchOptions.DefaultAddress));
            Assert.That(options.Port, Is.EqualTo(NetLaunchOptions.DefaultPort));
        }

        [Test]
        public void NoModeUsesGivenDefault()
        {
            var options = ParseOrFail("", NetLaunchMode.Client);

            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Client));
        }

        [TestCase("host", NetLaunchMode.Host)]
        [TestCase("server", NetLaunchMode.Server)]
        [TestCase("client", NetLaunchMode.Client)]
        [TestCase("SERVER", NetLaunchMode.Server)]
        public void ParsesMode(string value, NetLaunchMode expected)
        {
            var options = ParseOrFail("-mode " + value);

            Assert.That(options.Mode, Is.EqualTo(expected));
        }

        [Test]
        public void ParsesAllFlagsAmongUnityArguments()
        {
            var options = ParseOrFail("fps -batchmode -logFile - -mode client -address 192.168.0.10 -nographics -port 8888");

            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(options.Address, Is.EqualTo("192.168.0.10"));
            Assert.That(options.Port, Is.EqualTo((ushort)8888));
        }

        [Test]
        public void FlagsAreCaseInsensitive()
        {
            var options = ParseOrFail("-MODE server -Port 9000");

            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Server));
            Assert.That(options.Port, Is.EqualTo((ushort)9000));
        }

        [Test]
        public void ServerMayListenOnAnyAddress()
        {
            var options = ParseOrFail("-mode server -address 0.0.0.0");

            Assert.That(options.Address, Is.EqualTo(NetLaunchOptions.AnyAddress));
        }

        [TestCase("-mode")]
        [TestCase("-mode -port 7777")]
        [TestCase("-address")]
        [TestCase("-port")]
        [TestCase("-port -1")]
        public void RejectsMissingValue(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("requires a value"));
        }

        [TestCase("-mode peer")]
        [TestCase("-mode 1")]
        public void RejectsUnknownMode(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("-mode"));
        }

        [TestCase("-port 0")]
        [TestCase("-port 65536")]
        [TestCase("-port 70000")]
        [TestCase("-port abc")]
        [TestCase("-port 77.7")]
        [TestCase("-port +7777")]
        public void RejectsInvalidPort(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("-port"));
        }

        [Test]
        public void AcceptsPortBounds()
        {
            Assert.That(ParseOrFail("-port 1").Port, Is.EqualTo((ushort)1));
            Assert.That(ParseOrFail("-port 65535").Port, Is.EqualTo((ushort)65535));
        }

        [TestCase("-address localhost")]
        [TestCase("-address 1")]
        [TestCase("-address 1.2.3")]
        [TestCase("-address 1.2.3.4.5")]
        [TestCase("-address 256.0.0.1")]
        [TestCase("-address 01.2.3.4")]
        [TestCase("-address 1..2.3")]
        [TestCase("-address ::1")]
        public void RejectsInvalidAddress(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("-address"));
        }

        [Test]
        public void ClientCannotConnectToAnyAddress()
        {
            Assert.That(ParseError("-mode client -address 0.0.0.0"), Does.Contain("0.0.0.0"));
        }

        [TestCase("-mode host -mode client")]
        [TestCase("-address 127.0.0.1 -address 10.0.0.1")]
        [TestCase("-port 1 -port 2")]
        public void RejectsDuplicateFlags(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("more than once"));
        }

        [Test]
        public void RejectsNullArguments()
        {
            Assert.Throws<ArgumentNullException>(
                () => NetLaunchOptions.TryParse(null, NetLaunchMode.Host, out _, out _));
            Assert.Throws<ArgumentNullException>(() => NetLaunchOptions.HasModeFlag(null));
        }

        [Test]
        public void DetectsModeFlag()
        {
            Assert.That(NetLaunchOptions.HasModeFlag(Args("fps -batchmode -mode server")), Is.True);
            Assert.That(NetLaunchOptions.HasModeFlag(Args("fps -batchmode -port 7777")), Is.False);
        }

        [Test]
        public void ConstructorValidates()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.DefaultAddress, 0));
            Assert.Throws<ArgumentException>(
                () => new NetLaunchOptions(NetLaunchMode.Server, "not-an-ip", NetLaunchOptions.DefaultPort));
            Assert.Throws<ArgumentException>(
                () => new NetLaunchOptions(NetLaunchMode.Client, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new NetLaunchOptions((NetLaunchMode)99, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort));
        }

        [Test]
        public void FormatsForLogs()
        {
            var options = new NetLaunchOptions(NetLaunchMode.Server, "10.0.0.2", 7000);

            Assert.That(options.ToString(), Is.EqualTo("server 10.0.0.2:7000"));
        }
    }
}

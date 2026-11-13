using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetLaunchOptionsTests
    {
        private const string _serverId = "0123456789abcdef0123456789ABCDEF";

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

        [Test]
        public void TransportDefaultsToUdp()
        {
            var options = ParseOrFail("-mode client");

            Assert.That(options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(options.EosServerId, Is.Null);
            Assert.That(options.Endpoint, Is.EqualTo("127.0.0.1:7777"));
        }

        [TestCase("udp", NetTransportKind.Udp)]
        [TestCase("eos", NetTransportKind.Eos)]
        [TestCase("EOS", NetTransportKind.Eos)]
        public void ParsesTransport(string value, NetTransportKind expected)
        {
            var options = ParseOrFail("-mode server -transport " + value);

            Assert.That(options.Transport, Is.EqualTo(expected));
        }

        [Test]
        public void EosServerNeedsNoPeer()
        {
            var options = ParseOrFail("-mode server -transport eos");

            Assert.That(options.EosServerId, Is.Null);
            Assert.That(options.ToString(), Is.EqualTo("server eos"));
        }

        [Test]
        public void EosClientTakesServerId()
        {
            var options = ParseOrFail("-mode client -transport eos -eos-server " + _serverId);

            Assert.That(options.Transport, Is.EqualTo(NetTransportKind.Eos));
            Assert.That(options.EosServerId, Is.EqualTo(_serverId));
            Assert.That(options.ToString(), Is.EqualTo("client eos:" + _serverId));
        }

        [Test]
        public void RejectsUnknownTransport()
        {
            Assert.That(ParseError("-transport tcp"), Does.Contain("-transport"));
        }

        [Test]
        public void RejectsEosClientWithoutServerId()
        {
            Assert.That(ParseError("-mode client -transport eos"), Does.Contain("-eos-server"));
        }

        [TestCase("-mode client -eos-server " + _serverId)]
        [TestCase("-mode server -transport eos -eos-server " + _serverId)]
        [TestCase("-mode host -transport eos -eos-server " + _serverId)]
        public void RejectsServerIdWhereUnused(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("only used by a client"));
        }

        [TestCase("0123")]
        [TestCase("0123456789abcdef0123456789abcdeg")]
        [TestCase("0123456789abcdef0123456789abcdef0")]
        public void RejectsMalformedServerId(string value)
        {
            Assert.That(ParseError("-mode client -transport eos -eos-server " + value), Does.Contain("product user id"));
        }

        [TestCase("-transport")]
        [TestCase("-transport eos -mode client -eos-server")]
        public void RejectsMissingTransportValue(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("requires a value"));
        }

        [Test]
        public void RejectsDuplicateTransport()
        {
            Assert.That(ParseError("-transport eos -transport udp"), Does.Contain("more than once"));
        }

        [Test]
        public void WithTransportUdpDropsServerId()
        {
            var eos = ParseOrFail("-mode client -transport eos -eos-server " + _serverId + " -address 10.0.0.5 -port 9000");

            var udp = eos.WithTransport(NetTransportKind.Udp);

            Assert.That(udp.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(udp.EosServerId, Is.Null);
            Assert.That(udp.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(udp.Endpoint, Is.EqualTo("10.0.0.5:9000"));
            Assert.That(eos.WithTransport(NetTransportKind.Eos), Is.SameAs(eos));
        }

        [Test]
        public void ConstructorValidatesEosServerId()
        {
            Assert.Throws<ArgumentException>(() => new NetLaunchOptions(
                NetLaunchMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, null));
            Assert.Throws<ArgumentException>(() => new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, _serverId));
            Assert.Throws<ArgumentOutOfRangeException>(() => new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, (NetTransportKind)9, null));
        }

        [Test]
        public void RegistersNoLobbyByDefault()
        {
            var options = ParseOrFail("-mode server");

            Assert.That(options.RegistersLobby, Is.False);
            Assert.That(options.RegisterLobbyEndpoint, Is.Null);
        }

        [Test]
        public void ServerTakesTheEndpointToRegister()
        {
            var options = ParseOrFail("-mode server -address 0.0.0.0 -port 7777 -register-lobby 203.0.113.10:17777");

            Assert.That(options.RegistersLobby, Is.True);
            Assert.That(options.RegisterLobbyEndpoint, Is.EqualTo("203.0.113.10:17777"));
            Assert.That(options.Endpoint, Is.EqualTo("0.0.0.0:7777"));
            Assert.That(options.ToString(), Is.EqualTo("server 0.0.0.0:7777 lobby=203.0.113.10:17777"));
        }

        [Test]
        public void DedicatedServerDefaultModeCanRegister()
        {
            var options = ParseOrFail("-register-lobby 203.0.113.10:7777 -transport UDP", NetLaunchMode.Server);

            Assert.That(options.RegisterLobbyEndpoint, Is.EqualTo("203.0.113.10:7777"));
        }

        [TestCase("-register-lobby 203.0.113.10:7777")]
        [TestCase("-mode host -register-lobby 203.0.113.10:7777")]
        [TestCase("-mode client -register-lobby 203.0.113.10:7777")]
        public void OnlyAServerRegistersALobby(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.StartWith("-register-lobby is only used by -mode server"));
        }

        [Test]
        public void RegisteringNeedsUdp()
        {
            var error = ParseError("-mode server -transport eos -register-lobby 203.0.113.10:7777");

            Assert.That(error, Does.StartWith("-register-lobby needs -transport udp"));
        }

        [TestCase("203.0.113.10")]
        [TestCase("203.0.113.10:")]
        [TestCase(":7777")]
        [TestCase("0.0.0.0:7777")]
        [TestCase("example.com:7777")]
        [TestCase("203.0.113.10:0")]
        [TestCase("203.0.113.10:65536")]
        [TestCase("203.0.113.10:77a")]
        [TestCase("203.0.113.10:7777:1")]
        [TestCase("[::1]:7777")]
        public void RejectsMalformedRegisterEndpoint(string value)
        {
            var error = ParseError("-mode server -register-lobby " + value);

            Assert.That(error, Does.StartWith("-register-lobby must be the public IPv4 address and port"));
            Assert.That(error, Does.Contain(value));
        }

        [TestCase("-mode server -register-lobby")]
        [TestCase("-mode server -register-lobby -port 7777")]
        public void RejectsMissingRegisterEndpoint(string commandLine)
        {
            Assert.That(ParseError(commandLine), Does.Contain("requires a value"));
        }

        [Test]
        public void RejectsDuplicateRegisterEndpoint()
        {
            var error = ParseError("-mode server -register-lobby 203.0.113.10:7777 -register-lobby 203.0.113.11:7777");

            Assert.That(error, Does.Contain("more than once"));
        }

        [Test]
        public void WithTransportKeepsTheRegistrationOnUdpOnly()
        {
            var options = ParseOrFail("-mode server -register-lobby 203.0.113.10:7777");

            Assert.That(options.WithTransport(NetTransportKind.Udp), Is.SameAs(options));
            Assert.Throws<ArgumentException>(() => options.WithTransport(NetTransportKind.Eos));
        }

        [Test]
        public void ConstructorValidatesTheRegisterEndpoint()
        {
            var options = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, "10.0.0.2:7000");

            Assert.That(options.RegisterLobbyEndpoint, Is.EqualTo("10.0.0.2:7000"));
            Assert.Throws<ArgumentException>(() => new NetLaunchOptions(
                NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, "10.0.0.2:7000"));
            Assert.Throws<ArgumentException>(() => new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, null, "10.0.0.2:7000"));
            Assert.Throws<ArgumentException>(() => new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp, null, "10.0.0.2"));
        }

        [TestCase("127.0.0.1:7777", "127.0.0.1", 7777)]
        [TestCase("203.0.113.10:1", "203.0.113.10", 1)]
        [TestCase("203.0.113.10:65535", "203.0.113.10", 65535)]
        public void ParsesEndpoints(string text, string expectedAddress, int expectedPort)
        {
            var ok = NetLaunchOptions.TryParseEndpoint(text, out var address, out var port);

            Assert.That(ok, Is.True);
            Assert.That(address, Is.EqualTo(expectedAddress));
            Assert.That((int)port, Is.EqualTo(expectedPort));
            Assert.That(NetLaunchOptions.FormatEndpoint(address, port), Is.EqualTo(text));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("0.0.0.0:7777")]
        [TestCase("203.0.113.10")]
        [TestCase("203.0.113.10:+7777")]
        public void RejectsMalformedEndpoints(string text)
        {
            var ok = NetLaunchOptions.TryParseEndpoint(text, out var address, out var port);

            Assert.That(ok, Is.False);
            Assert.That(address, Is.Null);
            Assert.That((int)port, Is.Zero);
        }

        [Test]
        public void RecognizesProductUserIds()
        {
            Assert.That(NetLaunchOptions.IsProductUserId(_serverId), Is.True);
            Assert.That(NetLaunchOptions.IsProductUserId(null), Is.False);
            Assert.That(NetLaunchOptions.IsProductUserId(""), Is.False);
            Assert.That(NetLaunchOptions.IsProductUserId("g123456789abcdef0123456789abcdef"), Is.False);
        }
    }
}

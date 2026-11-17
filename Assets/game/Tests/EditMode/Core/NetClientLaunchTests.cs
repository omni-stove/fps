using System;
using NUnit.Framework;

namespace Shooter.Core.Tests
{
    // Unity の EditMode テストと dotnet/Shooter.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetClientLaunchTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        private static NetClientStart Resolve(string commandLine, out NetLaunchOptions server, out string error)
        {
            return NetClientLaunch.ResolveStart(Args(commandLine), out server, out error);
        }

        [Test]
        public void ModeClientConnectsAtOnceWithoutAnyStackFlag()
        {
            Assert.That(Resolve("Game.exe -mode client -address 10.0.0.5 -port 7800", out var server, out var error),
                Is.EqualTo(NetClientStart.Connect), error);
            Assert.That(server.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(server.Address, Is.EqualTo("10.0.0.5"));
            Assert.That(server.Port, Is.EqualTo(7800));
            Assert.That(server.Transport, Is.EqualTo(NetTransportKind.Udp));
        }

        [Test]
        public void ModeClientWithoutAnAddressConnectsToTheLocalServer()
        {
            Assert.That(Resolve("Game.exe -mode client", out var server, out var error), Is.EqualTo(NetClientStart.Connect), error);
            Assert.That(server.Endpoint, Is.EqualTo(NetLaunchOptions.DefaultAddress + ":" + NetLaunchOptions.DefaultPort));
        }

        [Test]
        public void ModeClientOverEosConnectsToTheGivenServer()
        {
            Assert.That(
                Resolve("Game.exe -mode client -transport eos -eos-server 0123456789abcdef0123456789abcdef -noeac", out var server, out var error),
                Is.EqualTo(NetClientStart.Connect), error);
            Assert.That(server.Transport, Is.EqualTo(NetTransportKind.Eos));
            Assert.That(server.EosServerId, Is.EqualTo("0123456789abcdef0123456789abcdef"));
        }

        [Test]
        public void NoModeLeavesTheConnectionToTheMenuAndSearchesTheLobbies()
        {
            Assert.That(Resolve("Game.exe", out var server, out var error), Is.EqualTo(NetClientStart.FromMenu), error);
            Assert.That(server, Is.Null, "the menu's quick match searches the server lobbies");

            Assert.That(Resolve("Game.exe -transport UDP", out server, out error), Is.EqualTo(NetClientStart.FromMenu), error);
            Assert.That(server, Is.Null);
        }

        [TestCase("Game.exe -address 10.0.0.5", "10.0.0.5:7777")]
        [TestCase("Game.exe -port 7800", "127.0.0.1:7800")]
        public void AServerAddressMakesTheMenuConnectThere(string commandLine, string endpoint)
        {
            Assert.That(Resolve(commandLine, out var server, out var error), Is.EqualTo(NetClientStart.FromMenu), error);
            Assert.That(server.Endpoint, Is.EqualTo(endpoint));
            Assert.That(NetClientLaunch.NamesServer(Args(commandLine)), Is.True);
        }

        [TestCase("Game.exe -lobby quickmatch")]
        [TestCase("Game.exe -LOBBY QuickMatch")]
        public void LobbyQuickMatchSearchesAtOnce(string commandLine)
        {
            Assert.That(Resolve(commandLine, out var server, out var error), Is.EqualTo(NetClientStart.QuickMatch), error);
            Assert.That(server, Is.Null);
        }

        [TestCase("Game.exe -mode server", "make server-dotnet")]
        [TestCase("Game.exe -mode host", "-mode")]
        [TestCase("Game.exe -mode client -map NetArena01", "-map")]
        [TestCase("Game.exe -mode client -net-latency 100", "-net-latency")]
        [TestCase("Game.exe -lobby create", "-lobby")]
        [TestCase("Game.exe -lobby join", "-lobby")]
        [TestCase("Game.exe -lobby quickmatch -mode client", "-mode")]
        [TestCase("Game.exe -lobby quickmatch -address 10.0.0.5", "-address")]
        [TestCase("Game.exe -lobby quickmatch -net-loss 5", "-net-loss")]
        [TestCase("Game.exe -lobby quickmatch -transport tcp", "-transport")]
        [TestCase("Game.exe -lobby quickmatch -transport udp -transport udp", "-transport")]
        [TestCase("Game.exe -lobby quickmatch -transport eos", "-eos-server")]
        [TestCase("Game.exe -transport eos", "-eos-server")]
        [TestCase("Game.exe -map NetTest", "-map")]
        [TestCase("Game.exe -bots 4", "-bots")]
        [TestCase("Game.exe -mode client -transport eos", "-eos-server")]
        public void RefusesWhatTheClientCannotRun(string commandLine, string mentioned)
        {
            Assert.That(Resolve(commandLine, out var server, out var error), Is.EqualTo(NetClientStart.Invalid));
            Assert.That(server, Is.Null);
            Assert.That(error, Does.Contain(mentioned));
            Assert.That(NetClientLaunch.ExitInvalidArguments, Is.EqualTo(2));
        }

        [Test]
        public void TheRemovedStackFlagIsIgnoredLikeOtherUnknownArguments()
        {
            Assert.That(Resolve("Game.exe -mode client -netstack v2", out var server, out var error), Is.EqualTo(NetClientStart.Connect), error);
            Assert.That(server.Mode, Is.EqualTo(NetLaunchMode.Client));
        }

        [Test]
        public void TheDefaultServerIsTheLocalUdpServer()
        {
            var server = NetClientLaunch.DefaultServer();
            Assert.That(server.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(server.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(server.Endpoint, Is.EqualTo(NetLaunchOptions.DefaultAddress + ":" + NetLaunchOptions.DefaultPort));
        }

        [Test]
        public void RejectsNull()
        {
            Assert.Throws<ArgumentNullException>(() => NetClientLaunch.ResolveStart(null, out _, out _));
        }
    }
}

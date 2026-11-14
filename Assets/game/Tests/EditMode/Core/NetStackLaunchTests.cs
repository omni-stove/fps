using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetStackLaunchTests
    {
        private static string[] Args(string commandLine)
        {
            return commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        }

        [TestCase("Game.exe -mode client", NetStackKind.Netcode)]
        [TestCase("Game.exe -mode client -netstack v1", NetStackKind.Netcode)]
        [TestCase("Game.exe -mode client -netstack v2", NetStackKind.V2)]
        [TestCase("Game.exe -NETSTACK V2 -mode client", NetStackKind.V2)]
        public void ReadsTheStack(string commandLine, NetStackKind expected)
        {
            Assert.That(NetStackLaunch.TryParse(Args(commandLine), out var stack, out var error), Is.True, error);
            Assert.That(stack, Is.EqualTo(expected));
        }

        [TestCase("Game.exe -netstack")]
        [TestCase("Game.exe -netstack v3")]
        [TestCase("Game.exe -netstack v2 -netstack v1")]
        public void RefusesAMissingUnknownOrRepeatedStack(string commandLine)
        {
            Assert.That(NetStackLaunch.TryParse(Args(commandLine), out _, out var error), Is.False);
            Assert.That(error, Does.Contain(NetStackLaunch.Flag));
            Assert.That(NetStackLaunch.RequestsV2(Args(commandLine)), Is.False);
        }

        [Test]
        public void AV2ClientKeepsTheAddressAndPortMeanings()
        {
            Assert.That(
                NetStackLaunch.TryResolveV2Client(Args("Game.exe -mode client -netstack v2 -address 10.0.0.5 -port 7800"), out var options, out var error),
                Is.True,
                error);
            Assert.That(options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(options.Address, Is.EqualTo("10.0.0.5"));
            Assert.That(options.Port, Is.EqualTo(7800));
        }

        [TestCase("Game.exe -netstack v2", "-mode client")]
        [TestCase("Game.exe -mode server -netstack v2", "make server-dotnet")]
        [TestCase("Game.exe -mode host -netstack v2", "make server-dotnet")]
        [TestCase("Game.exe -mode client -netstack v2 -map NetArena01", "-map")]
        [TestCase("Game.exe -mode client -netstack v2 -lobby quickmatch", "-lobby")]
        [TestCase("Game.exe -mode client -netstack v2 -net-latency 100", "-net-latency")]
        public void RefusesWhatTheV2ClientCannotRunYet(string commandLine, string mentioned)
        {
            Assert.That(NetStackLaunch.TryResolveV2Client(Args(commandLine), out var options, out var error), Is.False);
            Assert.That(options, Is.Null);
            Assert.That(error, Does.Contain(mentioned));
        }

        [Test]
        public void AV2ClientCanConnectOverEosToAServerId()
        {
            Assert.That(
                NetStackLaunch.TryResolveV2Client(
                    Args("Game.exe -mode client -netstack v2 -transport eos -eos-server 0123456789abcdef0123456789abcdef -noeac"),
                    out var options,
                    out var error),
                Is.True,
                error);
            Assert.That(options.Transport, Is.EqualTo(NetTransportKind.Eos));
            Assert.That(options.EosServerId, Is.EqualTo("0123456789abcdef0123456789abcdef"));
        }

        [Test]
        public void AV2EosClientNeedsTheServerId()
        {
            Assert.That(NetStackLaunch.TryResolveV2Client(Args("Game.exe -mode client -netstack v2 -transport eos"), out _, out var error), Is.False);
            Assert.That(error, Does.Contain("-eos-server"));
        }

        [Test]
        public void AV2ClientDoesNotLoadTheNetcodeScene()
        {
            Assert.That(NetStartup.ShouldLoadNetScene(false, false, Args("Game.exe -mode client -netstack v2")), Is.False);
            Assert.That(NetStartup.ShouldLoadNetScene(false, false, Args("Game.exe -mode client")), Is.True);
            Assert.That(NetStartup.ShouldLoadNetScene(false, true, Args("Game.exe -netstack v2")), Is.True);
        }
    }
}

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
        [TestCase("Game.exe -mode client -netstack v3")]
        [TestCase("Game.exe -mode client -netstack")]
        [TestCase("Game.exe -netstack v3")]
        [TestCase("Game.exe -netstack")]
        [TestCase("Game.exe -mode client -netstack v2 -netstack v2")]
        public void AWrongStackIsRefusedInsteadOfStartingTheNetcodeClient(string commandLine)
        {
            var start = NetStackLaunch.ResolveStart(Args(commandLine), false, out var options, out var error);

            Assert.That(start, Is.EqualTo(NetV2Start.Invalid));
            Assert.That(options, Is.Null);
            Assert.That(error, Does.Contain(NetStackLaunch.Flag));
            Assert.That(NetStackLaunch.ExitInvalidArguments, Is.EqualTo(2));
            Assert.That(NetStartup.ShouldLoadNetScene(false, false, Args(commandLine)), Is.False, "the Netcode scene must not start");
        }

        [Test]
        public void AV2ClientWithAModeConnectsAtOnceAndWithoutOneWaitsForTheMenu()
        {
            Assert.That(
                NetStackLaunch.ResolveStart(Args("Game.exe -mode client -netstack v2 -address 10.0.0.5"), false, out var now, out var error),
                Is.EqualTo(NetV2Start.Connect),
                error);
            Assert.That(now.Address, Is.EqualTo("10.0.0.5"));

            Assert.That(
                NetStackLaunch.ResolveStart(Args("Game.exe -netstack v2 -port 7800"), false, out var menu, out error),
                Is.EqualTo(NetV2Start.FromMenu),
                error);
            Assert.That(menu.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(menu.Port, Is.EqualTo(7800));
        }

        [TestCase("Game.exe -netstack v2 -lobby quickmatch", "-lobby")]
        [TestCase("Game.exe -netstack v2 -mode server", "make server-dotnet")]
        public void AV2StartWithArgumentsItCannotRunIsRefused(string commandLine, string mentioned)
        {
            Assert.That(NetStackLaunch.ResolveStart(Args(commandLine), false, out _, out var error), Is.EqualTo(NetV2Start.Invalid));
            Assert.That(error, Does.Contain(mentioned));
        }

        [TestCase("Game.exe -mode client")]
        [TestCase("Game.exe -mode client -netstack v1")]
        [TestCase("Game.exe")]
        public void WithoutV2NothingStarts(string commandLine)
        {
            Assert.That(NetStackLaunch.ResolveStart(Args(commandLine), false, out _, out var error), Is.EqualTo(NetV2Start.NotRequested), error);
        }

        [Test]
        public void ADedicatedServerIgnoresTheStack()
        {
            Assert.That(NetStackLaunch.ResolveStart(Args("Game.exe -netstack v3"), true, out _, out _), Is.EqualTo(NetV2Start.NotRequested));
        }
    }
}

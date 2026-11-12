using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetTransportPlanTests
    {
        private const string _serverId = "0123456789abcdef0123456789abcdef";

        private static readonly string[] _playerArgs = { "fps" };

        private static EosCredentials Credentials()
        {
            return new EosCredentials("product", "sandbox", "deployment", "client", "secret");
        }

        private static NetLaunchOptions EosClient()
        {
            return new NetLaunchOptions(NetLaunchMode.Client, "10.0.0.3", 7100, NetTransportKind.Eos, _serverId);
        }

        [Test]
        public void UdpIsKeptAsIs()
        {
            var requested = new NetLaunchOptions(NetLaunchMode.Server, NetLaunchOptions.AnyAddress, 7000);

            var plan = NetTransportPlan.Decide(requested, Credentials(), null);

            Assert.That(plan.Options, Is.SameAs(requested));
            Assert.That(plan.Credentials, Is.Null);
            Assert.That(plan.FellBackToUdp, Is.False);
            Assert.That(plan.MissingCredentials, Is.Empty);
        }

        [Test]
        public void EosWithCredentialsUsesEos()
        {
            var requested = EosClient();
            var credentials = Credentials();

            var plan = NetTransportPlan.Decide(requested, credentials, null);

            Assert.That(plan.Options, Is.SameAs(requested));
            Assert.That(plan.Credentials, Is.SameAs(credentials));
            Assert.That(plan.FellBackToUdp, Is.False);
        }

        [Test]
        public void EosWithoutCredentialsFallsBackToUdp()
        {
            var missing = new[] { EosCredentials.ClientSecretVariable };

            var plan = NetTransportPlan.Decide(EosClient(), null, missing);

            Assert.That(plan.FellBackToUdp, Is.True);
            Assert.That(plan.Credentials, Is.Null);
            Assert.That(plan.MissingCredentials, Is.EqualTo(missing));
            Assert.That(plan.Options.Transport, Is.EqualTo(NetTransportKind.Udp));
            Assert.That(plan.Options.EosServerId, Is.Null);
            Assert.That(plan.Options.Mode, Is.EqualTo(NetLaunchMode.Client));
            Assert.That(plan.Options.Address, Is.EqualTo("10.0.0.3"));
            Assert.That(plan.Options.Port, Is.EqualTo((ushort)7100));
        }

        [Test]
        public void FallbackWithoutMissingListReportsEmpty()
        {
            var requested = new NetLaunchOptions(
                NetLaunchMode.Server, NetLaunchOptions.AnyAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos, null);

            var plan = NetTransportPlan.Decide(requested, null, null);

            Assert.That(plan.FellBackToUdp, Is.True);
            Assert.That(plan.MissingCredentials, Is.Empty);
            Assert.That(plan.Options.Endpoint, Is.EqualTo("0.0.0.0:7777"));
        }

        [Test]
        public void AntiCheatIsDisabledUntilResolved()
        {
            var plan = NetTransportPlan.Decide(EosClient(), Credentials(), null);

            Assert.That(plan.AntiCheat, Is.Not.Null);
            Assert.That(plan.AntiCheat.Enabled, Is.False);
        }

        [Test]
        public void WithAntiCheatKeepsTheRest()
        {
            var plan = NetTransportPlan.Decide(EosClient(), Credentials(), null);
            Assert.That(
                AntiCheatSettings.TryResolve(_playerArgs, false, plan.Options, out var antiCheat, out _), Is.True);

            var withAntiCheat = plan.WithAntiCheat(antiCheat);

            Assert.That(withAntiCheat.AntiCheat, Is.SameAs(antiCheat));
            Assert.That(withAntiCheat.AntiCheat.Enabled, Is.True);
            Assert.That(withAntiCheat.Options, Is.SameAs(plan.Options));
            Assert.That(withAntiCheat.Credentials, Is.SameAs(plan.Credentials));
            Assert.That(plan.AntiCheat.Enabled, Is.False);
        }

        [Test]
        public void WithAntiCheatRejectsEnablingOverUdp()
        {
            var eosPlan = NetTransportPlan.Decide(EosClient(), Credentials(), null);
            Assert.That(AntiCheatSettings.TryResolve(_playerArgs, false, eosPlan.Options, out var enabled, out _), Is.True);
            var udpPlan = NetTransportPlan.Decide(EosClient(), null, null);

            Assert.Throws<ArgumentException>(() => udpPlan.WithAntiCheat(enabled));
            Assert.Throws<ArgumentNullException>(() => udpPlan.WithAntiCheat(null));
            Assert.That(udpPlan.WithAntiCheat(AntiCheatSettings.Disabled("off")).AntiCheat.Enabled, Is.False);
        }

        [Test]
        public void RejectsNullRequest()
        {
            Assert.Throws<ArgumentNullException>(() => NetTransportPlan.Decide(null, null, null));
        }
    }
}

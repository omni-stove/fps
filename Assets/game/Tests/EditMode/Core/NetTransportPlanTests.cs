using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    // Unity の EditMode テストと dotnet/Fps.Core.Tests の両方でコンパイルする。どちらの NUnit でも通る書き方に留める
    public sealed class NetTransportPlanTests
    {
        private const string _serverId = "0123456789abcdef0123456789abcdef";

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
        public void RejectsNullRequest()
        {
            Assert.Throws<ArgumentNullException>(() => NetTransportPlan.Decide(null, null, null));
        }
    }
}

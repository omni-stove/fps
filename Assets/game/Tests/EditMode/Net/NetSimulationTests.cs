using System;
using Fps.Core;
using NUnit.Framework;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Fps.Net.Tests
{
    /// <summary>
    /// <c>-net-latency</c> / <c>-net-loss</c> / <c>-net-jitter</c> を transport に効かせるかの判断と、起動ログ・警告の文。
    /// 開発ビルドとエディターで UnityTransport のときだけ効かせ、製品ビルドと EOS では理由を添えて無視することを確かめる。
    /// </summary>
    public sealed class NetSimulationTests
    {
        private GameObject _transportObject;

        [TearDown]
        public void TearDown()
        {
            if (_transportObject != null)
            {
                Object.DestroyImmediate(_transportObject);
            }
        }

        [Test]
        public void EditorIsASimulationBuild()
        {
            Assert.That(NetBootstrap.IsNetSimulationBuild, Is.True);
        }

        [Test]
        public void NothingIsAppliedWithoutTheFlags()
        {
            var options = Udp(0, 0, 0);

            Assert.That(NetBootstrap.DecideNetSimulation(options, true, CreateUnityTransport()), Is.EqualTo(NetSimulationOutcome.NotRequested));
            Assert.That(NetBootstrap.DecideNetSimulation(options, false, null), Is.EqualTo(NetSimulationOutcome.NotRequested),
                "a release build without the flags must not warn");
        }

        [TestCase(200, 0, 0)]
        [TestCase(0, 5, 0)]
        [TestCase(0, 0, 30)]
        public void AnyFlagIsAppliedToTheUnityTransportInASimulationBuild(int latencyMs, int lossPercent, int jitterMs)
        {
            var options = Udp(latencyMs, lossPercent, jitterMs);

            Assert.That(NetBootstrap.DecideNetSimulation(options, true, CreateUnityTransport()), Is.EqualTo(NetSimulationOutcome.Applied));
        }

        [Test]
        public void ReleaseBuildsIgnoreTheFlags()
        {
            var options = Udp(200, 5, 10);

            Assert.That(NetBootstrap.DecideNetSimulation(options, false, CreateUnityTransport()),
                Is.EqualTo(NetSimulationOutcome.IgnoredInReleaseBuild));
        }

        [Test]
        public void TransportsOtherThanUnityTransportIgnoreTheFlags()
        {
            var options = Eos(200, 5, 10);

            Assert.That(NetBootstrap.DecideNetSimulation(options, true, null), Is.EqualTo(NetSimulationOutcome.IgnoredForTransport));
        }

        [Test]
        public void StartupLogShowsTheValuesInEffect()
        {
            var log = NetBootstrap.FormatNetSimulationLog(Udp(200, 5, 10));

            Assert.That(log, Is.EqualTo("[NetSim] latency=200 loss=5 jitter=10 (ms, %, ms)"));
            Assert.That(log, Does.StartWith("[NetSim] latency=200"), "the issue's check greps for this prefix");
        }

        [Test]
        public void WarningsSayWhyTheFlagsWereIgnored()
        {
            var release = NetBootstrap.FormatNetSimulationWarning(NetSimulationOutcome.IgnoredInReleaseBuild, Udp(200, 5, 10));
            Assert.That(release, Does.StartWith(NetBootstrap.NetSimulationLogTag + " "));
            Assert.That(release, Does.Contain("development builds"));
            Assert.That(release, Does.Contain("latency=200 loss=5 jitter=10"));

            var transport = NetBootstrap.FormatNetSimulationWarning(NetSimulationOutcome.IgnoredForTransport, Eos(200, 0, 0));
            Assert.That(transport, Does.StartWith(NetBootstrap.NetSimulationLogTag + " "));
            Assert.That(transport, Does.Contain("transport=" + NetLaunchOptions.TransportName(NetTransportKind.Udp)));
            Assert.That(transport, Does.Contain("transport=" + NetLaunchOptions.TransportName(NetTransportKind.Eos)));

            Assert.That(NetBootstrap.FormatNetSimulationWarning(NetSimulationOutcome.Applied, Udp(200, 0, 0)), Is.Null);
            Assert.That(NetBootstrap.FormatNetSimulationWarning(NetSimulationOutcome.NotRequested, Udp(0, 0, 0)), Is.Null);
        }

        [Test]
        public void NullOptionsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => NetBootstrap.DecideNetSimulation(null, true, null));
            Assert.Throws<ArgumentNullException>(() => NetBootstrap.FormatNetSimulationLog(null));
            Assert.Throws<ArgumentNullException>(() => NetBootstrap.FormatNetSimulationWarning(NetSimulationOutcome.Applied, null));
        }

        private UnityTransport CreateUnityTransport()
        {
            if (_transportObject == null)
            {
                _transportObject = new GameObject("Transport");
            }

            var transport = _transportObject.GetComponent<UnityTransport>();
            return transport != null ? transport : _transportObject.AddComponent<UnityTransport>();
        }

        private static NetLaunchOptions Udp(int latencyMs, int lossPercent, int jitterMs)
        {
            return new NetLaunchOptions(
                NetLaunchMode.Client, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Udp,
                null, null, null, 0, latencyMs, lossPercent, jitterMs, 0);
        }

        private static NetLaunchOptions Eos(int latencyMs, int lossPercent, int jitterMs)
        {
            return new NetLaunchOptions(
                NetLaunchMode.Host, NetLaunchOptions.DefaultAddress, NetLaunchOptions.DefaultPort, NetTransportKind.Eos,
                null, null, null, 0, latencyMs, lossPercent, jitterMs, 0);
        }
    }
}

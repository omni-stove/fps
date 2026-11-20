using Shooter.Core;
using Shooter.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class TelemetryHostTests
    {
        private ControlSettings _appliedBefore;

        [SetUp]
        public void SetUp()
        {
            _appliedBefore = AppliedControlSettings.Current;
            // 既定の置き場所は persistentDataPath なので、テストの間に記録が書かれないよう同意を外しておく
            AppliedControlSettings.Apply(_appliedBefore.WithTelemetryConsent(false));
            DestroyHosts();
        }

        [TearDown]
        public void TearDown()
        {
            DestroyHosts();
            AppliedControlSettings.Apply(_appliedBefore);
        }

        [Test]
        public void InstallingTwiceLeavesOneAttachedHost()
        {
            var first = TelemetryHost.Install();
            var second = TelemetryHost.Install();

            Assert.That(second, Is.SameAs(first));
            Assert.That(Object.FindObjectsByType<TelemetryHost>(FindObjectsInactive.Include), Has.Length.EqualTo(1));
            Assert.That(TelemetryHost.Instance, Is.SameAs(first));
            Assert.That(first.gameObject.name, Is.EqualTo(TelemetryHost.ObjectName));
            Assert.That(first.Telemetry, Is.Not.Null);
            Assert.That(first.Telemetry.IsAttached, Is.True);
        }

        [Test]
        public void TheHostFollowsTheConsentInTheSettings()
        {
            var host = TelemetryHost.Install();

            Assert.That(host.Telemetry.Recorder.Consent, Is.False);
            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithTelemetryConsent(true));
            Assert.That(host.Telemetry.Recorder.Consent, Is.True);
            AppliedControlSettings.Apply(AppliedControlSettings.Current.WithTelemetryConsent(false));
            Assert.That(host.Telemetry.Recorder.Consent, Is.False);
        }

        [Test]
        public void AfterTheHostIsGoneInstallMakesANewOne()
        {
            var first = TelemetryHost.Install();
            DestroyHosts();

            var second = TelemetryHost.Install();

            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(Object.FindObjectsByType<TelemetryHost>(FindObjectsInactive.Include), Has.Length.EqualTo(1));
            Assert.That(second.Telemetry.IsAttached, Is.True);
        }

        // Edit Mode では OnDestroy が呼ばれないので、ログの購読を残さないよう記録を先に閉じる
        private static void DestroyHosts()
        {
            foreach (var host in Object.FindObjectsByType<TelemetryHost>(FindObjectsInactive.Include))
            {
                host.Telemetry?.Dispose();
                Object.DestroyImmediate(host.gameObject);
            }
        }
    }
}

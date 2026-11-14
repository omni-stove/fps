using System;
using System.Text;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class ConnectionApprovalTests
    {
        public const string Key = "0123456789abcdef0123456789abcdef";

        [Test]
        public void ApprovesTheSameVersionAndHandsBackTheKey()
        {
            var verdict = ConnectionApproval.Judge("0.2.0", ConnectionPayload.Encode("0.2.0", Key));

            Assert.That(verdict.Approved, Is.True);
            Assert.That(verdict.ClientVersion, Is.EqualTo("0.2.0"));
            Assert.That(verdict.RejoinKey, Is.EqualTo(Key));
            Assert.That(verdict.RejectReason, Is.Null);
            Assert.That(verdict.LogText, Is.Null);
        }

        [TestCase("0.1.0")]
        [TestCase("0.2.0-dirty")]
        [TestCase("0.2.0A")]
        public void RejectsAnotherVersionWithTheServerVersion(string clientVersion)
        {
            var verdict = ConnectionApproval.Judge("0.2.0", ConnectionPayload.Encode(clientVersion, Key));

            Assert.That(verdict.Approved, Is.False);
            Assert.That(verdict.ClientVersion, Is.EqualTo(clientVersion));
            Assert.That(verdict.RejoinKey, Is.Null);
            Assert.That(verdict.RejectReason, Is.EqualTo("fps:version-mismatch:0.2.0"));
            Assert.That(verdict.LogText, Does.Contain(clientVersion).And.Contain("0.2.0"));
        }

        [Test]
        public void VersionsAreCaseSensitive()
        {
            var verdict = ConnectionApproval.Judge("beta", ConnectionPayload.Encode("BETA", Key));

            Assert.That(verdict.Approved, Is.False);
        }

        [Test]
        public void RejectsUnreadablePayloadsIncludingLegacyKeyOnlyOnes()
        {
            foreach (var payload in new[] { null, Array.Empty<byte>(), RejoinKey.Encode(Key), Encoding.ASCII.GetBytes("0.2.0/a b") })
            {
                var verdict = ConnectionApproval.Judge("0.2.0", payload);

                Assert.That(verdict.Approved, Is.False);
                Assert.That(verdict.ClientVersion, Is.Null);
                Assert.That(verdict.RejectReason, Is.EqualTo("fps:rejected:" + ConnectionApproval.InvalidPayloadDetail));
                Assert.That(verdict.LogText, Does.Contain("unreadable"));
            }
        }

        [Test]
        public void ADrainingServerRejectsEveryoneAsDraining()
        {
            foreach (var payload in new[] { ConnectionPayload.Encode("0.2.0", Key), ConnectionPayload.Encode("0.1.0", Key), null })
            {
                var verdict = ConnectionApproval.Judge("0.2.0", payload, true);

                Assert.That(verdict.Approved, Is.False);
                Assert.That(verdict.RejoinKey, Is.Null);
                Assert.That(verdict.RejectReason, Is.EqualTo("fps:rejected:server-draining"));
                Assert.That(verdict.LogText, Does.Contain("draining"));
            }
        }

        [Test]
        public void ADrainingServerStillLogsTheClientVersion()
        {
            var verdict = ConnectionApproval.Judge("0.2.0", ConnectionPayload.Encode("0.1.0", Key), true);

            Assert.That(verdict.ClientVersion, Is.EqualTo("0.1.0"));
        }

        [Test]
        public void ANonDrainingServerJudgesAsBefore()
        {
            var verdict = ConnectionApproval.Judge("0.2.0", ConnectionPayload.Encode("0.2.0", Key), false);

            Assert.That(verdict.Approved, Is.True);
            Assert.That(verdict.RejoinKey, Is.EqualTo(Key));
        }

        [Test]
        public void ADrainingServerStillNeedsANormalizedVersion()
        {
            Assert.Throws<ArgumentException>(() => ConnectionApproval.Judge("v0.2.0", null, true));
        }

        [Test]
        public void TheRejectionsAreClassifiedOnTheClient()
        {
            var mismatch = ConnectionApproval.Judge("0.2.0", ConnectionPayload.Encode("0.1.0", Key));
            var unreadable = ConnectionApproval.Judge("0.2.0", null);

            Assert.That(DisconnectNotice.Classify(mismatch.RejectReason, false, "0.1.0").Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
            Assert.That(DisconnectNotice.Classify(unreadable.RejectReason, false, "0.1.0").Kind, Is.EqualTo(DisconnectKind.Rejected));
        }

        [Test]
        public void RequiresANormalizedServerVersion()
        {
            Assert.Throws<ArgumentException>(() => ConnectionApproval.Judge(null, null));
            Assert.Throws<ArgumentException>(() => ConnectionApproval.Judge("v0.2.0", null));
            Assert.Throws<ArgumentException>(() => ConnectionApproval.Judge("", null));
        }
    }
}

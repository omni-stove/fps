using System;
using System.Text;
using NUnit.Framework;

namespace Shooter.Core.Tests
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
            Assert.That(verdict.RejectReason, Is.EqualTo("shooter:version-mismatch:0.2.0"));
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
                Assert.That(verdict.RejectReason, Is.EqualTo("shooter:rejected:" + ConnectionApproval.InvalidPayloadDetail));
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
                Assert.That(verdict.RejectReason, Is.EqualTo("shooter:rejected:server-draining"));
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

        private static readonly NetMatchSettings _private = new NetMatchSettings(privateMatch: true, joinCode: "abc234");

        private static byte[] WithCode(string version, string joinCode)
        {
            return ConnectionPayload.Encode(version, Key, new ConnectionExtras("party-1", joinCode, 1200, null));
        }

        [TestCase("ABC234")]
        [TestCase("abc234")]
        [TestCase("aBc234")]
        public void APrivateServerApprovesTheJoinCodeInAnyCase(string joinCode)
        {
            var verdict = ConnectionApproval.Judge("0.2.0", WithCode("0.2.0", joinCode), false, _private);

            Assert.That(verdict.Approved, Is.True);
            Assert.That(verdict.ClientVersion, Is.EqualTo("0.2.0"));
            Assert.That(verdict.RejoinKey, Is.EqualTo(Key));
            Assert.That(verdict.RejectReason, Is.Null);
            Assert.That(verdict.LogText, Is.Null);
        }

        [Test]
        public void APrivateServerRejectsAWrongJoinCodeWithoutLoggingIt()
        {
            var verdict = ConnectionApproval.Judge("0.2.0", WithCode("0.2.0", "XYZ789"), false, _private);

            Assert.That(verdict.Approved, Is.False);
            Assert.That(verdict.ClientVersion, Is.EqualTo("0.2.0"));
            Assert.That(verdict.RejoinKey, Is.Null);
            Assert.That(verdict.RejectReason, Is.EqualTo("shooter:rejected:join-code-mismatch"));
            Assert.That(verdict.LogText, Is.EqualTo("wrong join code for the private match"));
        }

        [Test]
        public void APrivateServerRejectsAClientWithoutAJoinCode()
        {
            foreach (var payload in new[] { ConnectionPayload.Encode("0.2.0", Key), WithCode("0.2.0", null) })
            {
                var verdict = ConnectionApproval.Judge("0.2.0", payload, false, _private);

                Assert.That(verdict.Approved, Is.False);
                Assert.That(verdict.RejectReason, Is.EqualTo("shooter:rejected:join-code-mismatch"));
                Assert.That(verdict.LogText, Is.EqualTo("no join code for the private match"));
            }
        }

        [Test]
        public void APrivateServerWithoutACodeLetsNoOneIn()
        {
            var verdict = ConnectionApproval.Judge("0.2.0", WithCode("0.2.0", "ABC234"), false, new NetMatchSettings(privateMatch: true));

            Assert.That(verdict.Approved, Is.False);
            Assert.That(verdict.RejectReason, Is.EqualTo("shooter:rejected:join-code-mismatch"));
            Assert.That(verdict.LogText, Is.EqualTo("the private match has no join code to let anyone in"));
        }

        [Test]
        public void APublicServerIgnoresTheJoinCode()
        {
            var publicWithCode = new NetMatchSettings(joinCode: "ABC234");
            foreach (var payload in new[] { ConnectionPayload.Encode("0.2.0", Key), WithCode("0.2.0", "XYZ789") })
            {
                Assert.That(ConnectionApproval.Judge("0.2.0", payload, false, publicWithCode).Approved, Is.True);
                Assert.That(ConnectionApproval.Judge("0.2.0", payload, false, NetMatchSettings.Default).Approved, Is.True);
                Assert.That(ConnectionApproval.Judge("0.2.0", payload).Approved, Is.True);
            }
        }

        [Test]
        public void APrivateServerChecksTheVersionAndDrainingBeforeTheCode()
        {
            var mismatch = ConnectionApproval.Judge("0.2.0", WithCode("0.1.0", "XYZ789"), false, _private);
            var draining = ConnectionApproval.Judge("0.2.0", WithCode("0.2.0", "ABC234"), true, _private);
            var unreadable = ConnectionApproval.Judge("0.2.0", null, false, _private);

            Assert.That(mismatch.RejectReason, Is.EqualTo("shooter:version-mismatch:0.2.0"));
            Assert.That(draining.RejectReason, Is.EqualTo("shooter:rejected:server-draining"));
            Assert.That(unreadable.RejectReason, Is.EqualTo("shooter:rejected:" + ConnectionApproval.InvalidPayloadDetail));
        }

        [Test]
        public void TheJoinCodeRejectionIsClassifiedOnTheClient()
        {
            var verdict = ConnectionApproval.Judge("0.2.0", WithCode("0.2.0", "XYZ789"), false, _private);
            var notice = DisconnectNotice.Classify(verdict.RejectReason, false, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(notice.Detail, Is.EqualTo(DisconnectNotice.JoinCodeMismatchDetail));
        }

        [Test]
        public void RequiresTheMatchSettings()
        {
            Assert.Throws<ArgumentNullException>(() => ConnectionApproval.Judge("0.2.0", null, false, null));
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

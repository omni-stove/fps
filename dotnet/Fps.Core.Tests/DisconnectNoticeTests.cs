using System;
using NUnit.Framework;

namespace Fps.Core.Tests
{
    public sealed class DisconnectNoticeTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void TheServerStoppedReasonMeansTheServerStopped(bool wasConnected)
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.ServerStoppedReason, wasConnected, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.ServerStopped));
            Assert.That(notice.Message, Is.EqualTo("サーバーが止まりました。"));
            Assert.That(notice.WasConnected, Is.EqualTo(wasConnected));
        }

        [Test]
        public void SurroundingWhitespaceIsIgnored()
        {
            var notice = DisconnectNotice.Classify(" " + DisconnectNotice.ServerStoppedReason + "\n", true, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.ServerStopped));
        }

        [Test]
        public void AVersionMismatchNamesBothVersions()
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.VersionMismatchReason("0.2.0"), false, "0.1.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
            Assert.That(notice.ServerVersion, Is.EqualTo("0.2.0"));
            Assert.That(notice.Message, Does.StartWith("版が違います"));
            Assert.That(notice.Message, Does.Contain("サーバー 0.2.0").And.Contain("このゲーム 0.1.0"));
        }

        [TestCase("fps:version-mismatch:")]
        [TestCase("fps:version-mismatch:<b>red</b>")]
        [TestCase("fps:version-mismatch:v0.2.0")]
        public void AnUnreadableServerVersionIsLeftOut(string reason)
        {
            var notice = DisconnectNotice.Classify(reason, false, "0.1.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.VersionMismatch));
            Assert.That(notice.ServerVersion, Is.Null);
            Assert.That(notice.Message, Does.StartWith("版が違います。"));
            Assert.That(notice.Message, Does.Not.Contain("<b>"));
        }

        [Test]
        public void AnUnreadableLocalVersionIsLeftOut()
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.VersionMismatchReason("0.2.0"), false, null);

            Assert.That(notice.Message, Does.StartWith("版が違います。"));
        }

        [Test]
        public void ARejectionKeepsAReadableDetail()
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.RejectedReason("invalid-payload"), false, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(notice.Detail, Is.EqualTo("invalid-payload"));
            Assert.That(notice.Message, Is.EqualTo(DisconnectNotice.RejectedMessage));
        }

        [Test]
        public void ADrainingServerSaysItIsGettingReadyToStop()
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.RejectedReason(DisconnectNotice.ServerDrainingDetail), false, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(notice.Detail, Is.EqualTo("server-draining"));
            Assert.That(notice.Message, Does.StartWith("サーバーが止まる準備中"));
            Assert.That(notice.Describe(), Does.Contain("detail=server-draining"));
        }

        [Test]
        public void ARejectionDropsAnUnreadableDetail()
        {
            var notice = DisconnectNotice.Classify("fps:rejected:Not Allowed!", false, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.Rejected));
            Assert.That(notice.Detail, Is.Null);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Disconnected due to host shutting down.")]
        [TestCase("fps:something-new")]
        [TestCase("FPS:SERVER-STOPPED")]
        public void WithoutAKnownCodeAConnectedClientTimedOut(string reason)
        {
            var notice = DisconnectNotice.Classify(reason, true, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.TimedOut));
            Assert.That(notice.Message, Is.EqualTo(DisconnectNotice.TimedOutMessage));
            Assert.That(notice.RawReason, Is.EqualTo(reason ?? ""));
        }

        [Test]
        public void WithoutAKnownCodeAClientThatNeverConnectedCouldNotConnect()
        {
            var notice = DisconnectNotice.Classify(null, false, "0.2.0");

            Assert.That(notice.Kind, Is.EqualTo(DisconnectKind.TimedOut));
            Assert.That(notice.Message, Is.EqualTo(DisconnectNotice.ConnectTimedOutMessage));
        }

        [Test]
        public void DescribeListsWhatWasReceived()
        {
            var notice = DisconnectNotice.Classify(DisconnectNotice.VersionMismatchReason("0.2.0"), false, "0.1.0");

            Assert.That(notice.Describe(), Is.EqualTo(
                "kind=VersionMismatch connected=False server-version=0.2.0 reason=\"fps:version-mismatch:0.2.0\""));
            Assert.That(DisconnectNotice.Classify("", true, "0.1.0").Describe(), Is.EqualTo("kind=TimedOut connected=True"));
        }

        [Test]
        public void ReasonBuildersRejectWhatTheClientCouldNotRead()
        {
            Assert.Throws<ArgumentException>(() => DisconnectNotice.VersionMismatchReason("v0.2.0"));
            Assert.Throws<ArgumentException>(() => DisconnectNotice.VersionMismatchReason(null));
            Assert.Throws<ArgumentException>(() => DisconnectNotice.RejectedReason(""));
            Assert.Throws<ArgumentException>(() => DisconnectNotice.RejectedReason("Upper"));
            Assert.Throws<ArgumentException>(() => DisconnectNotice.RejectedReason(new string('a', DisconnectNotice.MaxDetailLength + 1)));
        }
    }
}

using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.Protocol;
using CoreReportReason = Shooter.Core.ReportReason;
using WireReportReason = Shooter.Protocol.ReportReason;

namespace Shooter.NetV2.Tests
{
    public sealed class ClientReportsTests
    {
        private static readonly string[] _sentLogs = { "[Report] sent target=4 reason=Cheating" };
        private static readonly string[] _notConnectedLogs = { "[Report] could not send target=4 reason=Spam (not connected)" };
        private readonly List<ReportRequestMessage> _sent = new List<ReportRequestMessage>();
        private readonly List<string> _logs = new List<string>();
        private bool _connected;

        [SetUp]
        public void SetUp()
        {
            _sent.Clear();
            _logs.Clear();
            _connected = true;
        }

        [Test]
        public void AReportIsSentAsAReportRequest()
        {
            var reports = new ClientReports(Send, _logs.Add);

            Assert.That(reports.Send(4, CoreReportReason.Cheating), Is.True);

            Assert.That(_sent.Count, Is.EqualTo(1));
            Assert.That(_sent[0].TargetPlayerId, Is.EqualTo(4));
            Assert.That(_sent[0].Reason, Is.EqualTo(WireReportReason.Cheating));
            Assert.That(_sent[0].Comment, Is.Empty);
            Assert.That(_logs, Is.EqualTo(_sentLogs));
        }

        [Test]
        public void TheSentRequestFitsTheProtocol()
        {
            var reports = new ClientReports(Send, _logs.Add);
            reports.Send(0, CoreReportReason.Other);

            var writer = new MessageWriter();
            Assert.DoesNotThrow(() => writer.WriteMessage(_sent[0]));
        }

        [Test]
        public void RepeatedReportsAreAllSentForTheServerToThinOut()
        {
            var reports = new ClientReports(Send, _logs.Add);

            reports.Send(4, CoreReportReason.Cheating);
            reports.Send(4, CoreReportReason.Cheating);

            Assert.That(_sent.Count, Is.EqualTo(2));
        }

        [Test]
        public void WhenNotConnectedTheReportIsNotSentAndThatIsLogged()
        {
            _connected = false;
            var reports = new ClientReports(Send, _logs.Add);

            Assert.That(reports.Send(4, CoreReportReason.Spam), Is.False);
            Assert.That(_logs, Is.EqualTo(_notConnectedLogs));
        }

        [TestCase(CoreReportReason.Cheating, WireReportReason.Cheating)]
        [TestCase(CoreReportReason.Abuse, WireReportReason.AbusiveChat)]
        [TestCase(CoreReportReason.Spam, WireReportReason.AbusiveChat)]
        [TestCase(CoreReportReason.OffensiveName, WireReportReason.Other)]
        [TestCase(CoreReportReason.Other, WireReportReason.Other)]
        public void EachReasonIsSentAsAWireReason(CoreReportReason reason, WireReportReason expected)
        {
            Assert.That(ClientReports.ToWire(reason), Is.EqualTo(expected));
        }

        [Test]
        public void EveryReasonHasAWireReason()
        {
            foreach (var reason in Enum.GetValues<CoreReportReason>())
            {
                Assert.DoesNotThrow(() => ClientReports.ToWire(reason), reason.ToString());
            }
        }

        [Test]
        public void BadValuesAreRefusedBeforeSending()
        {
            var reports = new ClientReports(Send, _logs.Add);

            Assert.Throws<ArgumentOutOfRangeException>(() => reports.Send(-1, CoreReportReason.Cheating));
            Assert.Throws<ArgumentOutOfRangeException>(() => reports.Send(4, (CoreReportReason)99));
            Assert.That(_sent, Is.Empty);
            Assert.That(_logs, Is.Empty);
        }

        [Test]
        public void TheConstructorRejectsMissingArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new ClientReports(null, _logs.Add));
            Assert.Throws<ArgumentNullException>(() => new ClientReports(Send, null));
        }

        private bool Send(IMessage message)
        {
            if (!_connected)
            {
                return false;
            }

            var request = (ReportRequestMessage)message;
            _sent.Add(new ReportRequestMessage { TargetPlayerId = request.TargetPlayerId, Reason = request.Reason, Comment = request.Comment });
            return true;
        }
    }
}

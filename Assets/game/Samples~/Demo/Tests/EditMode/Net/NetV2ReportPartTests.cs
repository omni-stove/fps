using System;
using System.Collections.Generic;
using NUnit.Framework;
using Shooter.NetV2;
using Shooter.Protocol;
using Shooter.Runtime;
using CoreReportReason = Shooter.Core.ReportReason;
using WireReportReason = Shooter.Protocol.ReportReason;

namespace Shooter.Net.Tests
{
    public sealed class NetV2ReportPartTests
    {
        private readonly List<ReportRequestMessage> _sent = new List<ReportRequestMessage>();
        private readonly List<string> _logs = new List<string>();
        private NetV2ReportPart _part;

        [SetUp]
        public void SetUp()
        {
            GameSignals.ReportSender.Clear();
            _sent.Clear();
            _logs.Clear();
            _part = new NetV2ReportPart(Send, _logs.Add);
        }

        [TearDown]
        public void TearDown()
        {
            _part.Dispose();
            GameSignals.ReportSender.Clear();
        }

        [Test]
        public void NothingIsPublishedBeforeTheMatchStarts()
        {
            Assert.That(GameSignals.ReportSender.HasValue, Is.False);
        }

        [Test]
        public void TheFirstSnapshotPublishesTheSender()
        {
            _part.Apply(new SnapshotMessage());

            Assert.That(GameSignals.ReportSender.Value, Is.SameAs(_part.ReportSender));
        }

        [Test]
        public void ThePublishedSenderSendsAReportRequest()
        {
            _part.Apply(new SnapshotMessage());

            GameSignals.ReportSender.Value.Report(7, CoreReportReason.Abuse);

            Assert.That(_sent.Count, Is.EqualTo(1));
            Assert.That(_sent[0].TargetPlayerId, Is.EqualTo(7));
            Assert.That(_sent[0].Reason, Is.EqualTo(WireReportReason.AbusiveChat));
            Assert.That(_logs, Has.Member("[Report] sent target=7 reason=Abuse"));
        }

        [Test]
        public void NoMessageIsTaken()
        {
            Assert.That(_part.Handle(new ReportRequestMessage()), Is.False);
            Assert.That(_part.Handle(new PingRemovedMessage()), Is.False);
        }

        [Test]
        public void DisposeClearsOnlyWhatThisPartPublishedAndStopsSending()
        {
            _part.Apply(new SnapshotMessage());
            var sender = GameSignals.ReportSender.Value;

            _part.Dispose();

            Assert.That(GameSignals.ReportSender.HasValue, Is.False);
            sender.Report(7, CoreReportReason.Cheating);
            Assert.That(_sent, Is.Empty, "a sender kept after leaving does not send");
            Assert.That(_logs, Has.Member("[Report] could not send target=7 reason=Cheating (left the match)"));

            var other = new NetV2ReportPart(Send, _logs.Add);
            other.Apply(new SnapshotMessage());
            _part.Dispose();
            Assert.That(GameSignals.ReportSender.Value, Is.SameAs(other.ReportSender));
            other.Dispose();
        }

        [Test]
        public void TheConstructorRejectsAMissingSend()
        {
            Assert.Throws<ArgumentNullException>(() => new NetV2ReportPart(null));
        }

        private bool Send(IMessage message)
        {
            var request = (ReportRequestMessage)message;
            _sent.Add(new ReportRequestMessage { TargetPlayerId = request.TargetPlayerId, Reason = request.Reason, Comment = request.Comment });
            return true;
        }
    }
}

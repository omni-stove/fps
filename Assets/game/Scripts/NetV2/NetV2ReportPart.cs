using System;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using ReportReason = Shooter.Core.ReportReason;

namespace Shooter.NetV2
{
    // ClientReports のある Shooter.NetV2.Client は Unity にも Shooter.Runtime にも依存しないので、GameSignals に出すのはこの部品が受け持つ。
    public sealed class NetV2ReportPart : INetV2ClientPart
    {
        private readonly ClientReports _reports;
        private readonly Action<string> _log;
        private readonly Sender _sender;
        private bool _started;
        private bool _disposed;

        public NetV2ReportPart(Func<IMessage, bool> send, Action<string> log = null)
        {
            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            _log = log ?? (line => Debug.Log(line));
            _reports = new ClientReports(send, _log);
            _sender = new Sender(this);
        }

        public IReportSender ReportSender => _sender;

        public bool Handle(IMessage message)
        {
            return false;
        }

        // 部品はつなぐ前に作られ、試合に入ったことは最初のスナップショットで分かるので、そこで送る口を出す。
        public void Apply(SnapshotMessage snapshot)
        {
            if (_disposed || snapshot == null || _started)
            {
                return;
            }

            _started = true;
            GameSignals.ReportSender.Publish(_sender);
        }

        public void Tick(float deltaSeconds)
        {
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.ReportSender.ClearIfCurrent(_sender);
        }

        // 部品そのものを渡すと UI から Handle や Dispose まで呼べてしまうので、送る口だけの包みを渡す。
        private sealed class Sender : IReportSender
        {
            private readonly NetV2ReportPart _part;

            public Sender(NetV2ReportPart part)
            {
                _part = part;
            }

            public void Report(int participantId, ReportReason reason)
            {
                if (_part._disposed)
                {
                    _part._log(ReportLog.NotSent(participantId, reason));
                    return;
                }

                _part._reports.Send(participantId, reason);
            }
        }
    }
}

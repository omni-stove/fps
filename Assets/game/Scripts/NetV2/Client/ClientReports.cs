using System;
using Shooter.Protocol;
using CoreReportReason = Shooter.Core.ReportReason;
using WireReportReason = Shooter.Protocol.ReportReason;

namespace Shooter.NetV2
{
    // 同じ相手への続けた通報を間引くのはサーバーの ServerReports なので、ここでは送れない値だけを止める。
    public sealed class ClientReports
    {
        private readonly Func<IMessage, bool> _send;
        private readonly Action<string> _log;
        private readonly ReportRequestMessage _request = new ReportRequestMessage();

        public ClientReports(Func<IMessage, bool> send, Action<string> log)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool Send(int participantId, CoreReportReason reason)
        {
            if (participantId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(participantId), participantId, "participantId must not be negative");
            }

            _request.TargetPlayerId = participantId;
            _request.Reason = ToWire(reason);
            _request.Comment = string.Empty;
            var sent = _send(_request);
            _log(ReportLog.Sent(participantId, reason, sent));
            return sent;
        }

        // 2 つの列挙は項目が違うので番号では写さない。番号で写すと Spam が AbusiveVoice のように別の理由として届く。
        public static WireReportReason ToWire(CoreReportReason reason)
        {
            switch (reason)
            {
                case CoreReportReason.Cheating:
                    return WireReportReason.Cheating;
                case CoreReportReason.Abuse:
                case CoreReportReason.Spam:
                    return WireReportReason.AbusiveChat;
                case CoreReportReason.OffensiveName:
                case CoreReportReason.Other:
                    return WireReportReason.Other;
                default:
                    throw new ArgumentOutOfRangeException(nameof(reason), reason, "reason must be a defined ReportReason");
            }
        }
    }
}

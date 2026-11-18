using Shooter.Core;

namespace Shooter.Runtime
{
    public interface IReportSender
    {
        void Report(int participantId, ReportReason reason);
    }
}

using System.Globalization;
using Shooter.Core;

namespace Shooter.Runtime
{
    public static class LockOnWarningLog
    {
        public static string StageChanged(LockOnWarningStatus? status)
        {
            if (!status.HasValue || !status.Value.IsWarning)
            {
                return $"{LockOnWarningPublisher.LogTag} lock-on warning cleared";
            }

            var stage = status.Value.Stage == LockOnWarningStage.Locked ? "locked" : "tracking";
            return $"{LockOnWarningPublisher.LogTag} lock-on warning {stage} bearing={status.Value.BearingDegrees.ToString("0", CultureInfo.InvariantCulture)}";
        }
    }
}

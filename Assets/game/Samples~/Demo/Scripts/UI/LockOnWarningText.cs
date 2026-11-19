using Shooter.Core;

namespace Shooter.Runtime
{
    public static class LockOnWarningText
    {
        public const string TrackingKey = "lockOn.tracking";

        public const string LockedKey = "lockOn.locked";

        public static LocalizedText Message(LockOnWarningStatus status)
        {
            switch (status.Stage)
            {
                case LockOnWarningStage.Tracking:
                    return new LocalizedText(TrackingKey);
                case LockOnWarningStage.Locked:
                    return new LocalizedText(LockedKey);
                default:
                    return default;
            }
        }
    }
}

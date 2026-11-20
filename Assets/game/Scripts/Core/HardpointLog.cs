using System.Globalization;

namespace Shooter.Core
{
    public static class HardpointLog
    {
        public static string MovedLog(int previousPointIndex, int nextPointIndex)
        {
            return ModeLog.Tag + " hardpoint moved from=" + DominationHudFormat.PointName(previousPointIndex)
                + " to=" + DominationHudFormat.PointName(nextPointIndex);
        }

        public static string StatusLog(int pointIndex, HardpointStatus status, Side? holder)
        {
            return ModeLog.Tag + " hardpoint point=" + DominationHudFormat.PointName(pointIndex)
                + " status=" + status
                + " holder=" + (holder.HasValue ? holder.Value.Id.ToString(CultureInfo.InvariantCulture) : "-");
        }
    }
}

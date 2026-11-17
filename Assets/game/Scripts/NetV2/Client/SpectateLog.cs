using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public static class SpectateLog
    {
        public const string Tag = "[Spectate]";

        public static string Changed(SpectateStatus? status)
        {
            return status.HasValue ? Tag + " net " + status.Value : Tag + " net stopped";
        }

        public static string Requested(SpectateStep step, bool sent)
        {
            return Tag + " net request step=" + step + (sent ? string.Empty : " (not connected; not sent)");
        }
    }
}

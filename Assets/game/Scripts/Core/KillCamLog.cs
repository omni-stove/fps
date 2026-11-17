using System.Globalization;

namespace Shooter.Core
{
    // オフラインとネット対戦で同じ形の行にし、smoke とテストが同じ行を探せるようにする。
    public static class KillCamLog
    {
        public const string Tag = "[KillCam]";

        public static string Replaying(string owner, string killer, float durationSeconds, float speed)
        {
            return Tag + " replaying " + killer + " for " + durationSeconds.ToString("F1", CultureInfo.InvariantCulture)
                + "s at " + speed.ToString("F2", CultureInfo.InvariantCulture) + "x on " + owner;
        }

        public static string Finished(string owner, string killer)
        {
            return Tag + " finished replaying " + killer + " on " + owner + "; switching to the killer view";
        }

        public static string NoRecord(string owner, string killer)
        {
            return Tag + " no record of " + killer + " for " + owner + "; switching to the killer view";
        }

        public static string NoKiller(string owner)
        {
            return Tag + " no killer for " + owner + "; skipping the replay";
        }

        public static string Stopped(string owner, string killer, string reason)
        {
            return Tag + " stopped replaying " + killer + " on " + owner + " (" + reason + ")";
        }

        public static string InvalidSpeed(string owner, float speed)
        {
            return Tag + " " + owner + " has playback speed "
                + speed.ToString(CultureInfo.InvariantCulture) + "; replaying at 1.00x";
        }
    }
}

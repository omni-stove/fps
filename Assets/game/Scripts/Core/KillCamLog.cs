using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// キルカム (<see cref="DeathCamera.KillCam"/>) のログの行。オフラインとネット対戦で同じ形にし、smoke とテストが同じ行を探せるようにする。
    /// 例: <c>[KillCam] replaying Enemy-1 for 4.0s at 1.00x on Player</c>。
    /// </summary>
    public static class KillCamLog
    {
        public const string Tag = "[KillCam]";

        /// <summary>killer の記録を再生し始めた。</summary>
        public static string Replaying(string owner, string killer, float durationSeconds, float speed)
        {
            return Tag + " replaying " + killer + " for " + durationSeconds.ToString("F1", CultureInfo.InvariantCulture)
                + "s at " + speed.ToString("F2", CultureInfo.InvariantCulture) + "x on " + owner;
        }

        /// <summary>最後まで再生したので、倒した相手を映す (<see cref="DeathCamera.Killer"/>) のに切り替える。</summary>
        public static string Finished(string owner, string killer)
        {
            return Tag + " finished replaying " + killer + " on " + owner + "; switching to the killer view";
        }

        /// <summary>killer の記録が無いので、再生せずにすぐ倒した相手を映す。</summary>
        public static string NoRecord(string owner, string killer)
        {
            return Tag + " no record of " + killer + " for " + owner + "; switching to the killer view";
        }

        /// <summary>倒した相手がいない (自滅・相手が消えた) ので再生しない。</summary>
        public static string NoKiller(string owner)
        {
            return Tag + " no killer for " + owner + "; skipping the replay";
        }

        /// <summary>再生の途中でやめた (リスポーン・試合の終わり・ルールの変更・相手が変わったなど)。</summary>
        public static string Stopped(string owner, string killer, string reason)
        {
            return Tag + " stopped replaying " + killer + " on " + owner + " (" + reason + ")";
        }

        /// <summary>演出が使えない再生の速さを返したので、1 倍で再生する。</summary>
        public static string InvalidSpeed(string presentation, float speed)
        {
            return Tag + " " + presentation + " returned playback speed "
                + speed.ToString(CultureInfo.InvariantCulture) + "; replaying at 1.00x";
        }
    }
}

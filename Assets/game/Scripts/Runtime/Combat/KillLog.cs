using Shooter.Core;

namespace Shooter.Runtime
{
    public static class KillLog
    {
        /// <summary>
        /// 例: <c>[Combat] Killed killer=1 victim=3 weapon=rifle headshot=False friendly=False assisters=2</c>。
        /// ネット対戦のクライアントが出す行と同じ形にし、オフラインとネット対戦のログを同じ検索で読めるようにする。
        /// </summary>
        public static string Killed(KillFeedEntry entry)
        {
            return Damageable.LogTag + " Killed " + entry;
        }
    }
}

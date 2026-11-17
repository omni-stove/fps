using Shooter.Core;

namespace Shooter.Runtime
{
    public static class KillLog
    {
        // ネット対戦のクライアントが出す行と同じ形にするのは、オフラインとネット対戦のログを同じ検索で読めるようにするため。
        public static string Killed(KillFeedEntry entry)
        {
            return Damageable.LogTag + " Killed " + entry;
        }
    }
}

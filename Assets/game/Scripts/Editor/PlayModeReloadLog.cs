namespace Shooter.Editor
{
    public static class PlayModeReloadLog
    {
        public const string LogTag = "[PlayModeReload]";

        public static string Deferred()
        {
            return $"{LogTag} scripts compiled during Play; they are reloaded when Play stops";
        }
    }
}

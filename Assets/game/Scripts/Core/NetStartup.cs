namespace Shooter.Core
{
    public static class NetStartup
    {
        public static bool IsDedicatedServerBuild
        {
            get
            {
#if UNITY_SERVER
                return true;
#else
                return false;
#endif
            }
        }

        public static bool IsHeadlessPlayer(bool isEditor, bool isBatchMode, bool isDedicatedServer)
        {
            return !isEditor && (isBatchMode || isDedicatedServer);
        }

        public static bool ShouldDisablePresentation(bool isDedicatedServer)
        {
            return isDedicatedServer;
        }
    }
}

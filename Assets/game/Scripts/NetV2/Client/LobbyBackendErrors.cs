namespace Shooter.NetV2
{
    public static class LobbyBackendErrors
    {
        // やり直しても成功しない要求なので、受け取った側は待たずに諦める。
        public const string NotSupported = "not-supported";

        public const string NotFound = "not-found";

        public static bool IsNotSupported(string reason)
        {
            return string.Equals(reason, NotSupported, System.StringComparison.Ordinal);
        }

        public static bool IsNotFound(string reason)
        {
            return string.Equals(reason, NotFound, System.StringComparison.Ordinal);
        }
    }
}

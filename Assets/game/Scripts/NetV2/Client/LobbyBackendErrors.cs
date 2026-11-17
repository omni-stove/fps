namespace Shooter.NetV2
{
    /// <summary><see cref="ILobbyBackend"/> が失敗の理由として渡す、決まった符号。</summary>
    public static class LobbyBackendErrors
    {
        /// <summary>このバックエンドではまだ使えない要求。やり直しても成功しないので、呼び出し側は待たずに諦める。</summary>
        public const string NotSupported = "not-supported";

        /// <summary>reason が <see cref="NotSupported"/> か。</summary>
        public static bool IsNotSupported(string reason)
        {
            return string.Equals(reason, NotSupported, System.StringComparison.Ordinal);
        }
    }
}

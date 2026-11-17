namespace Shooter.NetV2
{
    /// <summary><see cref="ILobbyBackend"/> が失敗の理由として渡す、決まった符号。</summary>
    public static class LobbyBackendErrors
    {
        /// <summary>このバックエンドではまだ使えない要求。やり直しても成功しないので、呼び出し側は待たずに諦める。</summary>
        public const string NotSupported = "not-supported";

        /// <summary>そのロビーが無い (コードの打ち間違い・閉じられた)、またはもう入っていない。</summary>
        public const string NotFound = "not-found";

        /// <summary>reason が <see cref="NotSupported"/> か。</summary>
        public static bool IsNotSupported(string reason)
        {
            return string.Equals(reason, NotSupported, System.StringComparison.Ordinal);
        }

        /// <summary>reason が <see cref="NotFound"/> か。</summary>
        public static bool IsNotFound(string reason)
        {
            return string.Equals(reason, NotFound, System.StringComparison.Ordinal);
        }
    }
}

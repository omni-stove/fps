namespace Shooter.NetV2
{
    /// <summary><see cref="PartySession"/> がパーティに入れなかった・パーティから外れた理由。UI はこれを文に変える。</summary>
    public enum PartyError
    {
        None,

        /// <summary>招待のコードが空か、使えない文字を含む。</summary>
        InvalidCode,

        /// <summary>ロビーのサービスにログインできなかった。</summary>
        LoginFailed,

        /// <summary>パーティを作れなかった。</summary>
        CreateFailed,

        /// <summary>招待のコードのパーティに入れなかった (無い・満員を含む)。</summary>
        JoinFailed,

        /// <summary>招待のコードのパーティが、版の違うビルドのもの。</summary>
        VersionMismatch,

        /// <summary>ロビーのサービスがパーティを使えない (<see cref="LobbyBackendErrors.NotSupported"/>)。</summary>
        Unavailable,

        /// <summary>パーティから外された、またはパーティが無くなった。</summary>
        Removed,

        /// <summary>ロビーのサービスのログインが切れた。</summary>
        LoggedOut,
    }
}

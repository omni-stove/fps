namespace Fps.Core
{
    /// <summary>ゲーム進行がメニューへ戻した (またはメニューに留めた) 理由の分け方。<see cref="MenuReturnReason.Kind"/>。</summary>
    public enum MenuReturnKind
    {
        /// <summary>サーバーとの接続を失った (つながらない・断られたを含む)。訳は <see cref="MenuReturnReason.Disconnect"/>。</summary>
        Disconnected,

        /// <summary>オンラインの試合を始められなかった (設定が足りない・ロビーを探せないなど)。訳は <see cref="MenuReturnReason.Detail"/>。</summary>
        OnlineFailed,
    }
}

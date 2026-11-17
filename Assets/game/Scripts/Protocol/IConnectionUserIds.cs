namespace Shooter.Protocol
{
    /// <summary>
    /// <see cref="IMatchTransport"/> と分けるのは、相手の EOS の ProductUserId を確かめてつなぐ通信路 (EOS の P2P) だけが実装でき、
    /// UDP のように知り得ない通信路に偽の値を返させないため。EAC はクライアントをこの ID で登録する。
    /// </summary>
    public interface IConnectionUserIds
    {
        /// <param name="connection">つながっている接続。</param>
        /// <param name="productUserId">EOS の ProductUserId の文字列表現。</param>
        /// <returns>知らない接続なら false。</returns>
        bool TryGetProductUserId(ConnectionId connection, out string productUserId);
    }
}

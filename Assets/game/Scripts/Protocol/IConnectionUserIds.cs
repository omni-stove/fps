namespace Shooter.Protocol
{
    // IMatchTransport と分けるのは、相手の EOS の ProductUserId を確かめてつなぐ通信路 (EOS の P2P) だけが実装でき、
    // UDP のように知り得ない通信路に偽の値を返させないため。
    public interface IConnectionUserIds
    {
        bool TryGetProductUserId(ConnectionId connection, out string productUserId);
    }
}

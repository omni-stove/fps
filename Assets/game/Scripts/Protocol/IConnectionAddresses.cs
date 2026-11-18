namespace Shooter.Protocol
{
    // IMatchTransport と分けるのは、EOS の P2P のように相手の IP を通信路が知らない (中継される) ものに偽の値を返させないため。
    public interface IConnectionAddresses
    {
        // IPv4 は IPv6 に写した形ではなく 203.0.113.5 の形で返す。
        bool TryGetRemoteAddress(ConnectionId connection, out string address);
    }
}

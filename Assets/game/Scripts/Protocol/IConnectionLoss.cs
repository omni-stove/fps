namespace Shooter.Protocol
{
    // IMatchTransport と分けるのは、落ちたパケットを数えない通信路に 0% を返させず、測れないことを呼ぶ側が知れるようにするため。
    public interface IConnectionLoss
    {
        // 0 から 100 の整数で返す。測り始めて間もなく数えた量が無いときは false を返す。
        bool TryGetLossPercent(ConnectionId connection, out int percent);
    }
}

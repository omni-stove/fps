namespace Shooter.Core
{
    // Core は通信路 (Shooter.Protocol.TransportEventKind) を参照できないので同じ並びを持ち、ファイルに書く値を固定する。
    public enum ReplayRecordKind : byte
    {
        Connected = 0,

        Disconnected = 1,

        Data = 2,
    }
}

namespace Shooter.Protocol
{
    /// <summary>
    /// サーバーが承認しなかったときに、切る前に返す理由。理由は <c>Shooter.Core.ConnectionVerdict.RejectReason</c> の符号で、
    /// クライアントは <c>Shooter.Core.DisconnectNotice.Classify</c> に渡して今と同じ文を出す。
    /// </summary>
    public sealed class ConnectRejectedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ConnectRejected;

        public byte Version => CurrentVersion;

        public string Reason { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteString(Reason, MatchProtocol.MaxReasonBytes);
        }

        public void Read(MessageReader reader)
        {
            Reason = reader.ReadString(MatchProtocol.MaxReasonBytes, Reason);
        }
    }
}

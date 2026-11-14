namespace Fps.Protocol
{
    /// <summary>
    /// サーバーが承認済みのクライアントを切る前に送る理由。止まるとき (<c>Fps.Core.DisconnectNotice.ServerStoppedReason</c>) と
    /// 入力が無くて切るとき (<c>IdleKickedReason</c>) に使う。
    /// </summary>
    public sealed class ServerDisconnectingMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ServerDisconnecting;

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

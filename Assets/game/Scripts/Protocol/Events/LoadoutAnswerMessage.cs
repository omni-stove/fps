namespace Fps.Protocol
{
    /// <summary><see cref="LoadoutRequestMessage"/> への返事。断ったなら訳 (ログに出す文) を付ける。</summary>
    public sealed class LoadoutAnswerMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        /// <summary>断った訳の最長のバイト数 (UTF-8)。</summary>
        public const int MaxReasonBytes = 512;

        public ushort Id => MessageIds.LoadoutAnswer;

        public byte Version => CurrentVersion;

        public bool Accepted { get; set; }

        /// <summary>断った訳。受け入れたなら空。</summary>
        public string Reason { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(Accepted);
            writer.WriteString(Accepted ? "" : Reason, MaxReasonBytes);
        }

        public void Read(MessageReader reader)
        {
            Accepted = reader.ReadBool();
            Reason = reader.ReadString(MaxReasonBytes, Reason);
        }
    }
}

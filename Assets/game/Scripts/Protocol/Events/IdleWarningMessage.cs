namespace Fps.Protocol
{
    /// <summary>
    /// 入力が無い持ち主に、切られるまでの残りを知らせる。入力が戻れば <see cref="Active"/> を false にして警告を消させる。
    /// </summary>
    public sealed class IdleWarningMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.IdleWarning;

        public byte Version => CurrentVersion;

        public bool Active { get; set; }

        /// <summary>切られるまでの秒。<see cref="Active"/> が false なら使わない。</summary>
        public float RemainingSeconds { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(Active);
            writer.WriteSingle(Active ? RemainingSeconds : 0f);
        }

        public void Read(MessageReader reader)
        {
            Active = reader.ReadBool();
            RemainingSeconds = reader.ReadFiniteSingle("idle remaining seconds");
        }
    }
}

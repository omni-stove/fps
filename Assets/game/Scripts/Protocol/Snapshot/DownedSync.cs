namespace Fps.Protocol
{
    /// <summary>プレイヤーのダウンの状態 (Netcode の <c>NetDownedState</c> の中身)。HUD に出す丸めた値で送る。</summary>
    public struct DownedSync
    {
        /// <summary><see cref="RevivePercent"/> の最大。</summary>
        public const int FullPercent = 100;

        public bool IsDowned { get; set; }

        /// <summary>失血で倒れるまでの残り秒 (表示用に切り上げた整数)。</summary>
        public int BleedoutSeconds { get; set; }

        /// <summary>蘇生の進み (0〜100)。</summary>
        public byte RevivePercent { get; set; }

        public static DownedSync None => default;

        public DownedSync(bool isDowned, int bleedoutSeconds, byte revivePercent)
        {
            IsDowned = isDowned;
            BleedoutSeconds = bleedoutSeconds;
            RevivePercent = revivePercent;
        }

        public bool SameAs(in DownedSync other)
        {
            return IsDowned == other.IsDowned && BleedoutSeconds == other.BleedoutSeconds && RevivePercent == other.RevivePercent;
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteBool(IsDowned);
            writer.WriteInt32(BleedoutSeconds);
            writer.WriteByte(RevivePercent);
        }

        internal static DownedSync Read(MessageReader reader)
        {
            return new DownedSync(
                reader.ReadBool(),
                reader.ReadInt32AtLeast(0, "bleedout seconds"),
                reader.ReadByteBelow(FullPercent + 1, "revive percent"));
        }
    }
}

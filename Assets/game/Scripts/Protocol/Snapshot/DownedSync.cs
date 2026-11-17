namespace Shooter.Protocol
{
    public struct DownedSync
    {
        public const int FullPercent = 100;

        public bool IsDowned { get; set; }

        public int BleedoutSeconds { get; set; }

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

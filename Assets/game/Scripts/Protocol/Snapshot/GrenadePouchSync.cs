namespace Fps.Protocol
{
    /// <summary>持ち主にだけ送るグレネードの残り (Netcode の <c>NetGrenadeState</c> の中身)。</summary>
    public struct GrenadePouchSync
    {
        /// <summary>試合のルールで投げられるか。</summary>
        public bool Enabled { get; set; }

        public int Remaining { get; set; }

        public GrenadePouchSync(bool enabled, int remaining)
        {
            Enabled = enabled;
            Remaining = remaining;
        }

        public bool SameAs(in GrenadePouchSync other)
        {
            return Enabled == other.Enabled && Remaining == other.Remaining;
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteBool(Enabled);
            writer.WriteInt32(Remaining);
        }

        internal static GrenadePouchSync Read(MessageReader reader)
        {
            return new GrenadePouchSync(reader.ReadBool(), reader.ReadInt32AtLeast(0, "grenades remaining"));
        }
    }
}

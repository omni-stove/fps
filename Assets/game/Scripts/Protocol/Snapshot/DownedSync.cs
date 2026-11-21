namespace Shooter.Protocol
{
    public struct DownedSync
    {
        public const int FullPercent = 100;

        public bool IsDowned { get; set; }

        public int BleedoutSeconds { get; set; }

        public byte RevivePercent { get; set; }

        // この人にとどめの技 (Execution) を刺している人。とどめの進み具合はサーバーの近接 (ServerMelee) が持つので、
        // 刺している側の HUD が自分の刺している相手と進み具合を読めるよう、刺されている人の行に載せる
        public bool HasExecutor { get; set; }

        public int ExecutorId { get; set; }

        public byte ExecutePercent { get; set; }

        public static DownedSync None => default;

        public DownedSync(bool isDowned, int bleedoutSeconds, byte revivePercent)
        {
            IsDowned = isDowned;
            BleedoutSeconds = bleedoutSeconds;
            RevivePercent = revivePercent;
            HasExecutor = false;
            ExecutorId = 0;
            ExecutePercent = 0;
        }

        // ダウンしていない人はとどめを刺されないので、刺している人を載せずにそのまま返す
        public DownedSync WithExecutor(int executorId, byte executePercent)
        {
            if (!IsDowned)
            {
                return this;
            }

            var copy = this;
            copy.HasExecutor = true;
            copy.ExecutorId = executorId;
            copy.ExecutePercent = executePercent > FullPercent ? (byte)FullPercent : executePercent;
            return copy;
        }

        public bool SameAs(in DownedSync other)
        {
            return IsDowned == other.IsDowned && BleedoutSeconds == other.BleedoutSeconds && RevivePercent == other.RevivePercent
                && HasExecutor == other.HasExecutor
                && (!HasExecutor || (ExecutorId == other.ExecutorId && ExecutePercent == other.ExecutePercent));
        }

        internal void Write(MessageWriter writer)
        {
            writer.WriteBool(IsDowned);
            writer.WriteInt32(BleedoutSeconds);
            writer.WriteByte(RevivePercent);
            writer.WriteBool(HasExecutor);
            if (HasExecutor)
            {
                if (!IsDowned)
                {
                    throw new System.InvalidOperationException("a player who is not downed cannot have an executor");
                }

                if (ExecutePercent > FullPercent)
                {
                    throw new System.InvalidOperationException("execute percent must be at most " + FullPercent);
                }

                writer.WriteInt32(ExecutorId);
                writer.WriteByte(ExecutePercent);
            }
        }

        internal static DownedSync Read(MessageReader reader)
        {
            var downed = new DownedSync(
                reader.ReadBool(),
                reader.ReadInt32AtLeast(0, "bleedout seconds"),
                reader.ReadByteBelow(FullPercent + 1, "revive percent"));
            if (!reader.ReadBool())
            {
                return downed;
            }

            var executorId = reader.ReadInt32();
            var percent = reader.ReadByteBelow(FullPercent + 1, "execute percent");
            if (!downed.IsDowned)
            {
                throw new MessageReadException("a player who is not downed has an executor");
            }

            return downed.WithExecutor(executorId, percent);
        }
    }
}

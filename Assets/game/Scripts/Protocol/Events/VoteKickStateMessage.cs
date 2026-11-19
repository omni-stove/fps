using System;

namespace Shooter.Protocol
{
    // 差分ではなくいつも全部を送るのは、小さいうえ、途中から入った人にも同じ 1 通で済むため (MapVoteStateMessage と同じ)。
    public sealed class VoteKickStateMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.VoteKickState;

        public byte Version => CurrentVersion;

        public int TargetPlayerId { get; set; }

        public int CallerPlayerId { get; set; }

        public int YesVotes { get; set; }

        public int RequiredVotes { get; set; } = 1;

        public float RemainingSeconds { get; set; }

        public VoteKickOutcome Outcome { get; set; }

        public void Write(MessageWriter writer)
        {
            if (TargetPlayerId < 0 || CallerPlayerId < 0)
            {
                throw new InvalidOperationException("a vote kick needs a target and a caller whose ids are not negative");
            }

            if (YesVotes < 0)
            {
                throw new InvalidOperationException("yes votes must not be negative");
            }

            if (RequiredVotes < 1)
            {
                throw new InvalidOperationException("a vote kick needs at least 1 vote to pass");
            }

            if (float.IsNaN(RemainingSeconds) || float.IsInfinity(RemainingSeconds) || RemainingSeconds < 0f)
            {
                throw new InvalidOperationException("remaining seconds must be finite and not negative");
            }

            if ((uint)Outcome >= (uint)EnumRange<VoteKickOutcome>.Count)
            {
                throw new InvalidOperationException("unknown vote kick outcome " + (int)Outcome);
            }

            writer.WriteInt32(TargetPlayerId);
            writer.WriteInt32(CallerPlayerId);
            writer.WriteInt32(YesVotes);
            writer.WriteInt32(RequiredVotes);
            writer.WriteSingle(RemainingSeconds);
            writer.WriteByte((byte)Outcome);
        }

        public void Read(MessageReader reader)
        {
            TargetPlayerId = reader.ReadInt32AtLeast(0, "vote kick target");
            CallerPlayerId = reader.ReadInt32AtLeast(0, "vote kick caller");
            YesVotes = reader.ReadInt32AtLeast(0, "vote kick yes votes");
            RequiredVotes = reader.ReadInt32AtLeast(1, "vote kick required votes");
            RemainingSeconds = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "vote kick remaining seconds");
            Outcome = (VoteKickOutcome)reader.ReadByteBelow(EnumRange<VoteKickOutcome>.Count, "vote kick outcome");
        }
    }
}

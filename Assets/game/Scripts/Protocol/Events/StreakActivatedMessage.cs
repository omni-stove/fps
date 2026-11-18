using System;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Protocol
{
    // 使った人の陣営も載せるのは、使った人がもう抜けていても、味方の報酬か敵の報酬かをクライアントが分けられるようにするため。
    public sealed class StreakActivatedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.StreakActivated;

        public byte Version => CurrentVersion;

        public int PlayerId { get; set; }

        public Team Team { get; set; }

        public string RewardId { get; set; } = "";

        public bool HasTarget { get; set; }

        public Vector3 Target { get; set; }

        public float DurationSeconds { get; set; }

        public void Write(MessageWriter writer)
        {
            if (PlayerId < 0)
            {
                throw new InvalidOperationException("the player who used a streak must have an id that is not negative");
            }

            if (string.IsNullOrEmpty(RewardId))
            {
                throw new InvalidOperationException("an activated streak needs a reward id");
            }

            if (!(DurationSeconds >= 0f) || float.IsInfinity(DurationSeconds))
            {
                throw new InvalidOperationException("streak duration must be finite and not negative");
            }

            writer.WriteInt32(PlayerId);
            writer.WriteByte((byte)Team);
            writer.WriteString(RewardId, MatchProtocol.MaxContentIdBytes);
            writer.WriteBool(HasTarget);
            if (HasTarget)
            {
                writer.WriteVector3(Target);
            }

            writer.WriteSingle(DurationSeconds);
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "streak user");
            Team = (Team)reader.ReadByteBelow(EnumRange<Team>.Count, "streak team");
            RewardId = reader.ReadString(MatchProtocol.MaxContentIdBytes, RewardId);
            if (RewardId.Length == 0)
            {
                throw new MessageReadException("an activated streak carries no reward id");
            }

            HasTarget = reader.ReadBool();
            Target = HasTarget ? reader.ReadFiniteVector3("streak target") : Vector3.Zero;
            DurationSeconds = reader.ReadFiniteSingle("streak duration");
            if (DurationSeconds < 0f)
            {
                throw new MessageReadException("streak duration must not be negative");
            }
        }
    }
}

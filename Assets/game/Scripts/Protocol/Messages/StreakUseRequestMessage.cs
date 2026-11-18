using System;
using System.Numerics;

namespace Shooter.Protocol
{
    // 狙う地点でなく向きを送るのは、地点をサーバーが自分の世界で当たりを取って決め、壁の向こうを指せないようにするため。
    public sealed class StreakUseRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.StreakUseRequest;

        public byte Version => CurrentVersion;

        public string RewardId { get; set; } = "";

        public Vector3 Direction { get; set; }

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(RewardId))
            {
                throw new InvalidOperationException("a streak use request needs a reward id");
            }

            writer.WriteString(RewardId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Direction);
        }

        public void Read(MessageReader reader)
        {
            RewardId = reader.ReadString(MatchProtocol.MaxContentIdBytes, RewardId);
            if (RewardId.Length == 0)
            {
                throw new MessageReadException("a streak use request carries no reward id");
            }

            Direction = reader.ReadFiniteVector3("streak direction");
        }
    }
}

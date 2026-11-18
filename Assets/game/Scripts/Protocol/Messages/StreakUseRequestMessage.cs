using System;
using System.Numerics;

namespace Shooter.Protocol
{
    // 地点は見下ろした地図で選ぶので、壁の向こうも選べる。サーバーは使った人からの水平の距離だけを確かめ、落ちる先は自分の世界で探す。
    public sealed class StreakUseRequestMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        public ushort Id => MessageIds.StreakUseRequest;

        public byte Version => CurrentVersion;

        public string RewardId { get; set; } = "";

        public bool HasTarget { get; set; }

        public Vector3 Target { get; set; }

        public float HeadingDegrees { get; set; }

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(RewardId))
            {
                throw new InvalidOperationException("a streak use request needs a reward id");
            }

            writer.WriteString(RewardId, MatchProtocol.MaxContentIdBytes);
            writer.WriteBool(HasTarget);
            if (HasTarget)
            {
                writer.WriteVector3(Target);
                writer.WriteSingle(HeadingDegrees);
            }
        }

        public void Read(MessageReader reader)
        {
            RewardId = reader.ReadString(MatchProtocol.MaxContentIdBytes, RewardId);
            if (RewardId.Length == 0)
            {
                throw new MessageReadException("a streak use request carries no reward id");
            }

            HasTarget = reader.ReadBool();
            Target = HasTarget ? reader.ReadFiniteVector3("streak target") : Vector3.Zero;
            HeadingDegrees = HasTarget ? reader.ReadFiniteSingle("streak heading") : 0f;
        }
    }
}

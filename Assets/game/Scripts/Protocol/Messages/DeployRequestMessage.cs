using System;
using System.Numerics;

namespace Shooter.Protocol
{
    // 向きでなく置く地点を送るのは、クライアントが見せた置き場所の見本と同じ所に置くため。届く距離と置ける面かはサーバーが確かめる。
    public sealed class DeployRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.DeployRequest;

        public byte Version => CurrentVersion;

        public string TypeId { get; set; } = "";

        public Vector3 Position { get; set; }

        public float Yaw { get; set; }

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(TypeId))
            {
                throw new InvalidOperationException("a deploy request needs a deployable type id");
            }

            writer.WriteString(TypeId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Position);
            writer.WriteSingle(Yaw);
        }

        public void Read(MessageReader reader)
        {
            TypeId = reader.ReadString(MatchProtocol.MaxContentIdBytes, TypeId);
            if (TypeId.Length == 0)
            {
                throw new MessageReadException("a deploy request carries no deployable type id");
            }

            Position = reader.ReadFiniteVector3("deploy position");
            Yaw = reader.ReadFiniteSingle("deploy yaw");
        }
    }
}

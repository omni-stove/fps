using System;

namespace Shooter.Protocol
{
    // 置いた要求 (DeployRequestMessage) と分けるのは、オフラインと同じく押し方 (短く押して離す) で起爆と置くを見分けるのがクライアントで、
    // サーバーは種類だけを受けて、その人の置いた同じ種類を全部起爆するため。
    public sealed class DetonateRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.DetonateRequest;

        public byte Version => CurrentVersion;

        public string TypeId { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (string.IsNullOrEmpty(TypeId))
            {
                throw new InvalidOperationException("a detonate request needs a deployable type id");
            }

            writer.WriteString(TypeId, MatchProtocol.MaxContentIdBytes);
        }

        public void Read(MessageReader reader)
        {
            TypeId = reader.ReadString(MatchProtocol.MaxContentIdBytes, TypeId);
            if (TypeId.Length == 0)
            {
                throw new MessageReadException("a detonate request carries no deployable type id");
            }
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Protocol
{
    public sealed class AreaEffectMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.AreaEffect;

        public byte Version => CurrentVersion;

        public int AreaId { get; set; }

        public string KindId { get; set; } = "";

        public Vector3 Center { get; set; }

        public float Radius { get; set; }

        /// <summary>0 なら効果が消えた。消えた知らせを別のメッセージにしないため。</summary>
        public float RemainingSeconds { get; set; }

        public void Write(MessageWriter writer)
        {
            if (AreaId < 0)
            {
                throw new InvalidOperationException("area id must not be negative");
            }

            writer.WriteInt32(AreaId);
            writer.WriteString(KindId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Center);
            writer.WriteSingle(Radius);
            writer.WriteSingle(RemainingSeconds);
        }

        public void Read(MessageReader reader)
        {
            AreaId = reader.ReadInt32AtLeast(0, "area effect id");
            KindId = reader.ReadString(MatchProtocol.MaxContentIdBytes, KindId);
            Center = reader.ReadFiniteVector3("area effect center");
            Radius = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "area effect radius");
            RemainingSeconds = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "area effect remaining seconds");
        }
    }
}

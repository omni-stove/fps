using System;

namespace Fps.Protocol
{
    public sealed class StatusEffectMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.StatusEffect;

        public byte Version => CurrentVersion;

        public int TargetId { get; set; }

        public string KindId { get; set; } = "";

        /// <summary>0 なら効果が切れた。切れた知らせを別のメッセージにしないため。</summary>
        public float RemainingSeconds { get; set; }

        public void Write(MessageWriter writer)
        {
            if (TargetId < 0)
            {
                throw new InvalidOperationException("target id must not be negative");
            }

            writer.WriteInt32(TargetId);
            writer.WriteString(KindId, MatchProtocol.MaxContentIdBytes);
            writer.WriteSingle(RemainingSeconds);
        }

        public void Read(MessageReader reader)
        {
            TargetId = reader.ReadInt32AtLeast(0, "status effect target");
            KindId = reader.ReadString(MatchProtocol.MaxContentIdBytes, KindId);
            RemainingSeconds = reader.ReadFiniteSingleBetween(0f, float.MaxValue, "status effect remaining seconds");
        }
    }
}

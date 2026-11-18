using System;

namespace Shooter.Protocol
{
    public sealed class ReportRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.ReportRequest;

        public byte Version => CurrentVersion;

        public int TargetPlayerId { get; set; }

        public ReportReason Reason { get; set; }

        public string Comment { get; set; } = "";

        public void Write(MessageWriter writer)
        {
            if (TargetPlayerId < 0)
            {
                throw new InvalidOperationException("a report needs a player id that is not negative");
            }

            writer.WriteInt32(TargetPlayerId);
            writer.WriteByte((byte)Reason);
            writer.WriteString(Comment, MatchProtocol.MaxReportCommentBytes);
        }

        public void Read(MessageReader reader)
        {
            TargetPlayerId = reader.ReadInt32AtLeast(0, "reported player");
            Reason = (ReportReason)reader.ReadByteBelow(EnumRange<ReportReason>.Count, "report reason");
            Comment = reader.ReadString(MatchProtocol.MaxReportCommentBytes, null);
        }
    }
}

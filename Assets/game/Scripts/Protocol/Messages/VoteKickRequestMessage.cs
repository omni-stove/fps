using System;

namespace Shooter.Protocol
{
    // 対象を接続 (ConnectionId) でなくプレイヤーの Id で送るのは、接続の番号はサーバーの中でしか意味を持たず、クライアントが知っているのはスナップショットに載るプレイヤーの Id だけのため。
    // 言い出すのも賛否を入れるのも同じ形にするのは、サーバーから見ればどちらも「その人を蹴ることに賛成か反対か」の 1 票で、進行中の投票が無ければ賛成の 1 票が言い出しになるため。
    public sealed class VoteKickRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.VoteKickRequest;

        public byte Version => CurrentVersion;

        public int TargetPlayerId { get; set; }

        public bool InFavor { get; set; }

        public void Write(MessageWriter writer)
        {
            if (TargetPlayerId < 0)
            {
                throw new InvalidOperationException("a vote kick needs a player id that is not negative");
            }

            writer.WriteInt32(TargetPlayerId);
            writer.WriteBool(InFavor);
        }

        public void Read(MessageReader reader)
        {
            TargetPlayerId = reader.ReadInt32AtLeast(0, "vote kick target");
            InFavor = reader.ReadBool();
        }
    }
}

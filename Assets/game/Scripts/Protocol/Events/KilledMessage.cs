using System;

namespace Shooter.Protocol
{
    /// <summary>全員に送るキルログの 1 行 (<c>Shooter.Core.KillFeedEntry</c> の中身)。自滅なら倒した人と倒された人が同じ。</summary>
    public sealed class KilledMessage : IMessage
    {
        public const byte CurrentVersion = 2;

        /// <summary>1 つに載せるアシストした参加者の最大数。</summary>
        public const int MaxAssisters = 16;

        private readonly int[] _assisterIds = new int[MaxAssisters];

        public ushort Id => MessageIds.Killed;

        public byte Version => CurrentVersion;

        public int KillerId { get; set; }

        public int VictimId { get; set; }

        public string WeaponId { get; set; } = "";

        public bool Headshot { get; set; }

        /// <summary>味方を倒したか。</summary>
        public bool Friendly { get; set; }

        /// <summary>アシストした参加者の数。</summary>
        public int AssisterCount { get; private set; }

        /// <summary>index 番目にアシストした参加者の Id。</summary>
        public int AssisterId(int index)
        {
            if ((uint)index >= (uint)AssisterCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be below " + AssisterCount);
            }

            return _assisterIds[index];
        }

        public void ClearAssisters()
        {
            AssisterCount = 0;
        }

        /// <exception cref="InvalidOperationException">もう <see cref="MaxAssisters"/> 人いる。</exception>
        public void AddAssister(int playerId)
        {
            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "assister id must not be negative");
            }

            if (AssisterCount == MaxAssisters)
            {
                throw new InvalidOperationException("a kill carries at most " + MaxAssisters + " assisters");
            }

            _assisterIds[AssisterCount] = playerId;
            AssisterCount++;
        }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(KillerId);
            writer.WriteInt32(VictimId);
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteBool(Headshot);
            writer.WriteBool(Friendly);
            writer.WriteByte((byte)AssisterCount);
            for (var i = 0; i < AssisterCount; i++)
            {
                writer.WriteInt32(_assisterIds[i]);
            }
        }

        public void Read(MessageReader reader)
        {
            AssisterCount = 0;
            KillerId = reader.ReadInt32AtLeast(0, "killer");
            VictimId = reader.ReadInt32AtLeast(0, "victim");
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            Headshot = reader.ReadBool();
            Friendly = reader.ReadBool();
            var count = reader.ReadByteBelow(MaxAssisters + 1, "assister count");
            for (var i = 0; i < count; i++)
            {
                _assisterIds[i] = reader.ReadInt32AtLeast(0, "assister");
            }

            AssisterCount = count;
        }
    }
}

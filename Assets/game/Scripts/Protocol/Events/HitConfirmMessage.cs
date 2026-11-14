using Fps.Core;

namespace Fps.Protocol
{
    /// <summary>撃った人にだけ送る、自分の弾が当たった知らせ (ヒットマーカー)。</summary>
    public sealed class HitConfirmMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.HitConfirm;

        public byte Version => CurrentVersion;

        public int Damage { get; set; }

        public HitZone Zone { get; set; }

        public bool Killed { get; set; }

        public bool Downed { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(Damage);
            writer.WriteByte((byte)Zone);
            writer.WriteBool(Killed);
            writer.WriteBool(Downed);
        }

        public void Read(MessageReader reader)
        {
            Damage = reader.ReadInt32AtLeast(0, "damage");
            Zone = (HitZone)reader.ReadByteBelow(EnumRange<HitZone>.Count, "hit zone");
            Killed = reader.ReadBool();
            Downed = reader.ReadBool();
        }
    }
}

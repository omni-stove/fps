using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>撃たれた人にだけ送る、受けたダメージと撃ってきた位置 (被弾の向きの表示)。</summary>
    public sealed class DamageTakenMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.DamageTaken;

        public byte Version => CurrentVersion;

        /// <summary>撃ってきた位置が分かるか (落下などなら false)。</summary>
        public bool HasSource { get; set; }

        public Vector3 SourcePosition { get; set; }

        public int Amount { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteBool(HasSource);
            if (HasSource)
            {
                writer.WriteVector3(SourcePosition);
            }

            writer.WriteInt32(Amount);
        }

        public void Read(MessageReader reader)
        {
            HasSource = reader.ReadBool();
            SourcePosition = HasSource ? reader.ReadFiniteVector3("damage source") : default;
            Amount = reader.ReadInt32AtLeast(0, "damage amount");
        }
    }
}

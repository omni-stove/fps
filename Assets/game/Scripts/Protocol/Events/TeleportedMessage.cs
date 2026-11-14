using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>
    /// サーバーがプレイヤーを置き直した (出現・リスポーン) 知らせ。全員に送り、他の人の補間の履歴を捨てさせ、持ち主には予測を置き直させる。
    /// 置き直すとカバー・回避・乗り越えをやめ、立って宙にいる状態になる。
    /// </summary>
    public sealed class TeleportedMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.Teleported;

        public byte Version => CurrentVersion;

        public int PlayerId { get; set; }

        /// <summary>置き直したサーバーの tick。</summary>
        public int Tick { get; set; }

        public Vector3 Position { get; set; }

        /// <summary>置き直した後の向き (Y 軸まわり、ラジアン)。</summary>
        public float Yaw { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteInt32(PlayerId);
            writer.WriteInt32(Tick);
            writer.WriteVector3(Position);
            writer.WriteSingle(Yaw);
        }

        public void Read(MessageReader reader)
        {
            PlayerId = reader.ReadInt32AtLeast(0, "player id");
            Tick = reader.ReadInt32AtLeast(0, "teleport tick");
            Position = reader.ReadFiniteVector3("teleport position");
            Yaw = reader.ReadFiniteSingle("teleport yaw");
        }
    }
}

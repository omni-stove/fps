using System.Numerics;

namespace Fps.Protocol
{
    /// <summary>
    /// 持ち主が撃ったときの要求。当たり判定はサーバーが、撃った tick と補間の割合で他の人を巻き戻して行う。
    /// 何発目か (<see cref="ShotIndex"/>・<see cref="ShotGeneration"/>) はサーバーが自分の番号と突き合わせるだけに使う。
    /// </summary>
    public sealed class FireRequestMessage : IMessage
    {
        public const byte CurrentVersion = 1;

        public ushort Id => MessageIds.FireRequest;

        public byte Version => CurrentVersion;

        public string WeaponId { get; set; } = "";

        public Vector3 Origin { get; set; }

        public Vector3 Direction { get; set; }

        /// <summary>撃ったときにクライアントが表示していた tick。</summary>
        public int ClientTick { get; set; }

        /// <summary>表示していた tick と次の tick の間の割合 (0〜1)。</summary>
        public float InterpolationAlpha { get; set; }

        public int ShotIndex { get; set; }

        public int ShotGeneration { get; set; }

        public void Write(MessageWriter writer)
        {
            writer.WriteString(WeaponId, MatchProtocol.MaxContentIdBytes);
            writer.WriteVector3(Origin);
            writer.WriteVector3(Direction);
            writer.WriteInt32(ClientTick);
            writer.WriteSingle(InterpolationAlpha);
            writer.WriteInt32(ShotIndex);
            writer.WriteInt32(ShotGeneration);
        }

        public void Read(MessageReader reader)
        {
            WeaponId = reader.ReadString(MatchProtocol.MaxContentIdBytes, WeaponId);
            Origin = reader.ReadFiniteVector3("shot origin");
            Direction = reader.ReadFiniteVector3("shot direction");
            ClientTick = reader.ReadInt32AtLeast(0, "client tick");
            InterpolationAlpha = reader.ReadFiniteSingle("interpolation alpha");
            ShotIndex = reader.ReadInt32AtLeast(0, "shot index");
            ShotGeneration = reader.ReadInt32AtLeast(0, "shot generation");
        }
    }
}

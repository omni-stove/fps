namespace Fps.Core
{
    /// <summary><see cref="AntiCheatPacket"/> の種類。値は通信路に載るので変えない。</summary>
    public enum AntiCheatPacketKind : byte
    {
        /// <summary>壊れている・知らない種類・版が違う。</summary>
        Invalid = 0,

        /// <summary>接続直後に互いの EAC の有効・無効を伝える。</summary>
        Hello = 1,

        /// <summary>EAC の SDK が相手へ送るよう求めたメッセージをそのまま運ぶ。</summary>
        Message = 2,
    }
}

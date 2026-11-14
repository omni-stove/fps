namespace Fps.Core
{
    /// <summary>クライアントがサーバーとやり取りする手順。コマンドライン引数 <c>-netstack v1|v2</c> に対応する。</summary>
    public enum NetStackKind
    {
        /// <summary>Netcode for GameObjects (今の既定)。</summary>
        Netcode,

        /// <summary>Fps.Protocol と LiteNetLib で .NET のサーバーへつなぐ新しい手順。</summary>
        V2,
    }
}

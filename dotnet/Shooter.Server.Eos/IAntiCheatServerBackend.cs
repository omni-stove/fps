using System;

namespace Shooter.Server.Eos
{
    /// <summary>
    /// SDK の型を外に出さずハンドルと文字列で受け渡すのは、SDK の無いビルドとテストでも EAC のモジュールを動かせるようにするため。
    /// イベントは <see cref="Tick"/> の中で呼ぶ。
    /// </summary>
    internal interface IAntiCheatServerBackend : IDisposable
    {
        /// <summary>引数はハンドルと、そのクライアントへ送るメッセージ。メッセージは呼ばれている間だけ読める。</summary>
        event Action<long, ArraySegment<byte>> MessageToClient;

        /// <summary>引数はハンドル、クライアントを外すべきか、ログに出す理由。</summary>
        event Action<long, bool, string> ClientActionRequired;

        /// <summary>引数はハンドルと認証の状態の名前。</summary>
        event Action<long, string> ClientAuthStatusChanged;

        bool Begin(out string error);

        bool RegisterClient(long handle, string productUserId, out string error);

        bool UnregisterClient(long handle, out string error);

        bool ReceiveMessageFromClient(long handle, ArraySegment<byte> message, out string error);

        void Tick();

        bool End(out string error);
    }
}

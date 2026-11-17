using System;

namespace Shooter.Server.Eos
{
    // SDK の型を外に出さずハンドルと文字列で受け渡すのは、SDK の無いビルドとテストでも EAC のモジュールを動かせるようにするため。
    internal interface IAntiCheatServerBackend : IDisposable
    {
        // 渡すメッセージは呼ばれている間だけ読める。
        event Action<long, ArraySegment<byte>> MessageToClient;

        event Action<long, bool, string> ClientActionRequired;

        event Action<long, string> ClientAuthStatusChanged;

        bool Begin(out string error);

        bool RegisterClient(long handle, string productUserId, out string error);

        bool UnregisterClient(long handle, out string error);

        bool ReceiveMessageFromClient(long handle, ArraySegment<byte> message, out string error);

        void Tick();

        bool End(out string error);
    }
}

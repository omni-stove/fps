using System;

namespace Shooter.NetV2
{
    // Unity にも EOS のプラグインにも依存しないアセンブリから ClientAntiCheat を動かし、テストで差し替えられるよう、SDK の呼び出しをここに分ける。
    public interface IAntiCheatClientSession
    {
        // sendToServer に渡すメッセージは呼ばれている間だけ読める。
        bool BeginSession(Action<ArraySegment<byte>> sendToServer, Action<string> fail, out string reason);

        bool ReceiveMessageFromServer(ArraySegment<byte> message, out string reason);

        void EndSession();
    }
}

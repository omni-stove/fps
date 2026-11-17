using System;

namespace Shooter.NetV2
{
    /// <summary>
    /// EAC の SDK (Anti-Cheat Client の ClientServer モード) の側。Unity にも EOS のプラグインにも依存しないアセンブリから
    /// <see cref="ClientAntiCheat"/> を動かし、テストで差し替えられるよう、SDK の呼び出しをここに分ける。
    /// </summary>
    public interface IAntiCheatClientSession
    {
        /// <param name="sendToServer">EAC がサーバーへ送るよう求めたメッセージを渡す先。渡したメッセージは呼ばれている間だけ読める。</param>
        /// <param name="fail">こちらの整合性の違反などで続けられなくなったとき、理由を渡す先。</param>
        /// <param name="reason">始められない理由 (ブートストラッパーを通さずに起動した、EOS にログインしていない、など)。</param>
        bool BeginSession(Action<ArraySegment<byte>> sendToServer, Action<string> fail, out string reason);

        /// <returns>EAC が受け取らなかったら false と理由。</returns>
        bool ReceiveMessageFromServer(ArraySegment<byte> message, out string reason);

        /// <summary>2 度呼んでもよい。</summary>
        void EndSession();
    }
}

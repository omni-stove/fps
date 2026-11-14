using System;

namespace Fps.Protocol
{
    /// <summary>
    /// <see cref="EosMatchTransport"/> が使う EOS の P2P の呼び出しを、SDK の型を出さずに表したもの。相手は ProductUserId の文字列で表す。
    /// サーバーは EOS の C# SDK を、クライアントは PlayEveryWare のプラグインを呼ぶので、SDK を呼ぶ部分だけをそれぞれが実装し、
    /// つなぐ手順・メッセージの分割・切断の理由の運び方は <see cref="EosMatchTransport"/> の 1 か所に置く。
    /// </summary>
    /// <remarks>
    /// どの呼び出しも呼ぶ側のスレッドで返し、SDK の通知は <see cref="Tick"/> の中で溜めて <see cref="TryTakeNotice"/> から出す。
    /// 接続は <see cref="EosMatchTransport.SocketName"/> のソケットで開く。
    /// </remarks>
    public interface IEosP2P
    {
        /// <summary>
        /// プラットフォームを用意して Device ID でログインし始める。ログインの結果は <see cref="EosP2PNoticeKind.SignedIn"/> か
        /// <see cref="EosP2PNoticeKind.SignInFailed"/> で知らせる。ログインした後に接続の通知を受け始める。
        /// </summary>
        /// <param name="acceptsIncoming">サーバーとして、相手からの接続の要求 (<see cref="EosP2PNoticeKind.ConnectionRequested"/>) を受けるか。</param>
        /// <param name="problem">始められないときの理由 (SDK が無い・プラットフォームを作れないなど)。</param>
        /// <returns>始められたか。</returns>
        bool TryBegin(bool acceptsIncoming, out string problem);

        /// <summary>SDK を進める。通知はこの中で溜まる。</summary>
        void Tick();

        /// <summary>溜まった通知を 1 つ取り出す。無ければ false。</summary>
        bool TryTakeNotice(out EosP2PNotice notice);

        /// <summary>
        /// 届いたパケットを 1 つ buffer に読む。無い・読めないときは false。
        /// </summary>
        /// <param name="buffer"><see cref="EosMatchTransport.MaxPacketSize"/> バイト以上の受け取り先。</param>
        /// <param name="remoteUserId">送ってきた相手。</param>
        /// <param name="channel">届いたチャンネル。</param>
        /// <param name="length">読んだバイト数。</param>
        bool TryReceivePacket(byte[] buffer, out string remoteUserId, out byte channel, out int length);

        /// <summary>
        /// 1 つのパケットを送る。相手との接続を勝手に開き直さない (接続は <see cref="AcceptConnection"/> の手順でだけ開く)。
        /// </summary>
        /// <param name="remoteUserId">送る相手。</param>
        /// <param name="channel">送るチャンネル。</param>
        /// <param name="reliable">必ず届け、同じチャンネルの中で順に届けるか。</param>
        /// <param name="packet"><see cref="EosMatchTransport.MaxPacketSize"/> バイト以下のパケット。</param>
        /// <param name="problem">送れなかったときの理由。</param>
        /// <returns>送る列に積めたか。</returns>
        bool SendPacket(string remoteUserId, byte channel, bool reliable, ArraySegment<byte> packet, out string problem);

        /// <summary>相手との接続を受け入れる。クライアントがサーバーに対して呼ぶと、サーバーに接続の要求が届く。</summary>
        void AcceptConnection(string remoteUserId);

        /// <summary>相手との接続を閉じる。相手には <see cref="EosP2PNoticeKind.ConnectionClosed"/> が届く。</summary>
        void CloseConnection(string remoteUserId);

        /// <summary>すべての接続を閉じ、通知の登録を外し、プラットフォームを手放して止める。2 度呼んでもよい。</summary>
        void CloseAll();
    }
}

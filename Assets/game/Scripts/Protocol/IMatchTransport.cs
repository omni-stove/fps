using System;

namespace Fps.Protocol
{
    /// <summary>
    /// 試合の通信路の差し込み口。UDP・EOS の P2P・テスト用のメモリ上の通信路などが実装する。
    /// 受け取りは呼ぶ側の tick から <see cref="TryReceive"/> で引き出す形にし、実装のスレッドから呼び返さない。
    /// 1 つの実装はサーバーとして待ち受けるか、クライアントとして 1 か所へつなぐかのどちらかだけを行う。
    /// </summary>
    public interface IMatchTransport : IDisposable
    {
        /// <summary>待ち受けかつなぎ (の試み) の最中か。<see cref="Shutdown"/> の後は false。</summary>
        bool IsActive { get; }

        /// <summary>
        /// サーバーとして待ち受ける。address の意味は実装が決める (UDP なら待ち受ける IPv4 アドレス、EOS なら使わない)。
        /// </summary>
        /// <exception cref="ArgumentException">address を待ち受けるアドレスとして読めない。</exception>
        /// <exception cref="InvalidOperationException">すでに待ち受けかつなぎの最中か、待ち受けを始められない (ポートが使われているなど)。</exception>
        void Listen(string address, ushort port);

        /// <summary>
        /// クライアントとしてつなぐ。つながったら <see cref="TransportEventKind.Connected"/>、つながらなければ
        /// <see cref="TransportEventKind.Disconnected"/> が <see cref="TryReceive"/> から出てくる。
        /// address の意味は実装が決める (UDP ならサーバーの IPv4 アドレス、EOS ならサーバーの ProductUserId)。
        /// </summary>
        /// <exception cref="InvalidOperationException">すでに待ち受けかつなぎの最中。</exception>
        void Connect(string address, ushort port);

        /// <summary>
        /// その接続を切り、reason (<c>Fps.Core.DisconnectNotice</c> の符号など。null か空なら知らせない) を相手に知らせる
        /// (相手の <see cref="TransportEvent.Reason"/> になる)。
        /// 切れたら <see cref="TransportEventKind.Disconnected"/> が出てくる。知らない Id か、もう切っている最中なら何もしない。
        /// </summary>
        /// <exception cref="ArgumentException">reason が UTF-8 で <see cref="TransportEvent.MaxReasonBytes"/> バイトを超える。</exception>
        void Disconnect(ConnectionId connection, string reason);

        /// <summary>
        /// その接続へ送る。payload の中身は呼んだ時点で写すので、戻った後に書き換えてよい。
        /// 知らない Id、まだつながっていないか切っている最中の接続へは捨てる。
        /// </summary>
        /// <exception cref="ArgumentException">payload が空か、<see cref="GetMaxPayloadSize"/> を超える。</exception>
        void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload);

        /// <summary>そのチャンネルで 1 度に送れる最長のバイト数。</summary>
        int GetMaxPayloadSize(TransportChannel channel);

        /// <summary>
        /// その接続の往復時間 (ミリ秒) を返す。つながっていない・まだ測っていない接続なら false。
        /// </summary>
        bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds);

        /// <summary>溜まった出来事を 1 つ取り出す。無ければ false。</summary>
        bool TryReceive(out TransportEvent transportEvent);

        /// <summary>
        /// すべての接続を切り、reason を相手に知らせて止める。溜まった出来事は捨て、以後は何も出てこない。2 度呼んでもよい。
        /// </summary>
        /// <param name="reason">切る理由 (サーバーが止まるなら <c>Fps.Core.DisconnectNotice.ServerStoppedReason</c>)。null か空なら知らせない。</param>
        /// <exception cref="ArgumentException">reason が UTF-8 で <see cref="TransportEvent.MaxReasonBytes"/> バイトを超える。</exception>
        void Shutdown(string reason);
    }
}

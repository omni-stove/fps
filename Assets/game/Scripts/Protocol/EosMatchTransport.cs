using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using Shooter.Core;

namespace Shooter.Protocol
{
    /// <summary>
    /// EOS の P2P で <see cref="IMatchTransport"/> を実装する。相手は IP アドレスではなく ProductUserId で指定する。
    /// SDK の呼び出しは <see cref="IEosP2P"/> に任せるので、サーバー (EOS の C# SDK) とクライアント (PlayEveryWare のプラグイン) で同じ手順になる。
    /// </summary>
    /// <remarks>
    /// <para>
    /// ソケット名とチャンネルは Netcode の EOS の transport (<c>Shooter.NetEos.EosP2PTransport</c>) と同じにする:
    /// 落ちてよいものはチャンネル 0、順に届くものはチャンネル 2 で送り、チャンネル 3 は Easy Anti-Cheat のために空けておく。
    /// どのパケットも <see cref="PacketSplitter"/> の形にし、その中身の先頭 1 バイトで、データ・切断の知らせ・往復時間を測る ping/pong を分ける。
    /// </para>
    /// <para>
    /// EOS は接続を閉じた理由を相手に渡せず、閉じると送りかけのパケットを捨てうるので、切るときは理由を載せた知らせを順に届くチャンネルで
    /// データの後ろに送り、受け取った側が閉じるのを待つ (<see cref="EosTransportOptions.CloseLingerMilliseconds"/> を過ぎたらこちらから閉じる)。
    /// </para>
    /// <para>
    /// サーバーは <see cref="Listen"/> の中でログインを終えるまで待つ。待ち受けられたかを戻り値と例外で知らせ、UDP と同じく
    /// 戻った時点で待ち受けているようにするため。クライアントは画面を止めないよう、ログインからつながるまでを <see cref="TryReceive"/> で進める。
    /// </para>
    /// </remarks>
    public sealed class EosMatchTransport : IMatchTransport, IConnectionUserIds
    {
        /// <summary>P2P のソケット名。英数字 1〜32 文字で、サーバーとクライアントで同じであること。</summary>
        public const string SocketName = "shooter";

        /// <summary>EOS P2P の 1 パケットの上限 (EOS_P2P_MAX_PACKET_SIZE)。</summary>
        public const int MaxPacketSize = 1170;

        public const byte UnreliableChannel = 0;
        public const byte ReliableChannel = 2;

        /// <summary>分けて送るので 1 パケットに収まらなくてよい。1 つのメッセージの最長 (<see cref="MessageWriter"/>) に合わせる。</summary>
        public const int MaxReliablePayloadSize = MessageWriter.DefaultMaxLength;

        /// <summary>落ちてよいものは分けると 1 つ落ちただけで全部を失うので、1 パケットに収まる長さに限る。</summary>
        public const int MaxUnreliablePayloadSize = MaxPacketSize - PacketSplitter.WholeHeaderSize - _frameHeaderSize;

        private const string _logTag = "[Net]";
        private const int _frameHeaderSize = 1;
        private const byte _dataFrame = 0;
        private const byte _closeFrame = 1;
        private const byte _pingFrame = 2;
        private const byte _pongFrame = 3;
        private const int _stampSize = 4;
        private const int _maxPendingMessagesPerPeer = 32;
        private const int _maxPendingBytesPerPeer = 2 * 1024 * 1024;
        private const int _maxPacketsPerPump = 4096;
        private const int _pollMilliseconds = 5;
        private const int _maxRoundTripMilliseconds = 60000;
        private const double _roundTripSmoothing = 0.125;

        private static readonly Stopwatch _sharedClock = Stopwatch.StartNew();

        private readonly IEosP2P _p2p;
        private readonly EosTransportOptions _options;
        private readonly Action<string> _log;
        private readonly Func<long> _clock;
        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private readonly Dictionary<string, Peer> _peersByUser = new Dictionary<string, Peer>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, Peer> _peersById = new Dictionary<ulong, Peer>();
        private readonly List<Peer> _scratch = new List<Peer>();
        private readonly List<EosP2PNotice> _closedNotices = new List<EosP2PNotice>();
        private readonly PacketSplitter _splitter = new PacketSplitter(MaxPacketSize);
        private readonly byte[] _receiveBuffer = new byte[MaxPacketSize];
        private readonly byte[] _frame = new byte[_frameHeaderSize + MaxReliablePayloadSize];
        private readonly byte[] _stamp = new byte[_stampSize];
        private readonly Action<ArraySegment<byte>> _sendPacket;
        private Role _role;
        private bool _active;
        private bool _signedIn;
        private bool _stopping;
        private string _signInError;
        private string _serverUserId;
        private long _connectDeadline;
        private bool _clientConnected;
        private bool _clientEnded;
        private ulong _lastId;
        private byte[] _lentBuffer;
        private string _sendTarget;
        private bool _sendReliable;
        private int _sendFailures;
        private int _malformedPackets;

        /// <param name="p2p">SDK を呼ぶ部分。この通信路が持ち、止めるときに <see cref="IEosP2P.CloseAll"/> を呼ぶ。</param>
        /// <param name="options">待ち時間の調整値。</param>
        /// <param name="log"><c>[Net]</c> 付きの 1 行を受け取る先。null なら出さない。</param>
        /// <param name="clockMilliseconds">単調に増えるミリ秒の時計。null ならプロセスの時計。テストで時間を進めるために差し替える。</param>
        public EosMatchTransport(IEosP2P p2p, EosTransportOptions options, Action<string> log, Func<long> clockMilliseconds = null)
        {
            _p2p = p2p ?? throw new ArgumentNullException(nameof(p2p));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _log = log ?? (_ => { });
            _clock = clockMilliseconds ?? (() => _sharedClock.ElapsedMilliseconds);
            _sendPacket = SendPacketToTarget;
        }

        private enum Role
        {
            None,
            Server,
            Client,
        }

        public bool IsActive => _active;

        /// <summary>ログインした自分の ProductUserId。ログイン前は null。</summary>
        public string LocalUserId { get; private set; }

        private long Now => _clock();

        /// <summary>EOS では待ち受けるアドレスとポートを使わない。ログインできるまで待ち、できなければ投げる。</summary>
        /// <exception cref="InvalidOperationException">すでに待ち受けかつなぎの最中か、ログインできなかった。</exception>
        public void Listen(string address, ushort port)
        {
            ThrowIfActive();
            _role = Role.Server;
            if (!_p2p.TryBegin(true, out var beginError))
            {
                _role = Role.None;
                throw new InvalidOperationException(beginError);
            }

            _active = true;
            var deadline = Now + _options.SignInTimeoutMilliseconds;
            while (true)
            {
                _p2p.Tick();
                DrainNotices();
                HandleClosedNotices();
                if (_signedIn)
                {
                    return;
                }

                var failure = _signInError;
                if (failure == null && Now >= deadline)
                {
                    failure = "the EOS login did not finish within " + Seconds(_options.SignInTimeoutMilliseconds);
                }

                if (failure != null)
                {
                    StopNetwork();
                    throw new InvalidOperationException(failure);
                }

                Thread.Sleep(_pollMilliseconds);
            }
        }

        /// <summary>address はサーバーの ProductUserId。port は使わない。</summary>
        /// <exception cref="ArgumentException">address が ProductUserId の形でない。</exception>
        public void Connect(string address, ushort port)
        {
            ThrowIfActive();
            var serverUserId = address?.Trim().ToLowerInvariant();
            if (!NetLaunchOptions.IsProductUserId(serverUserId))
            {
                throw new ArgumentException("'" + address + "' is not an EOS product user id", nameof(address));
            }

            _role = Role.Client;
            _active = true;
            _clientConnected = false;
            _clientEnded = false;
            _serverUserId = serverUserId;
            AddPeer(serverUserId);
            _connectDeadline = Now + _options.ConnectTimeoutMilliseconds;
            if (!_p2p.TryBegin(false, out var error))
            {
                _log(_logTag + " EOS cannot start: " + error);
                EndClient("");
            }
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
            var bytes = TransportReason.Encode(reason);
            if (!_peersById.TryGetValue(connection.Value, out var peer) || peer.Closing)
            {
                return;
            }

            if (!peer.Reported)
            {
                EndClient(reason ?? "");
                return;
            }

            BeginClose(peer, reason ?? "", bytes);
        }

        public void Send(ConnectionId connection, TransportChannel channel, ArraySegment<byte> payload)
        {
            var max = GetMaxPayloadSize(channel);
            if (payload.Array == null || payload.Count == 0 || payload.Count > max)
            {
                throw new ArgumentException(
                    "payload must be 1-" + max.ToString(CultureInfo.InvariantCulture) + " bytes on the " + channel + " channel (got "
                    + payload.Count.ToString(CultureInfo.InvariantCulture) + ")",
                    nameof(payload));
            }

            if (_peersById.TryGetValue(connection.Value, out var peer) && peer.Reported && !peer.Closing)
            {
                SendFrame(peer, _dataFrame, payload, channel == TransportChannel.Reliable);
            }
        }

        public int GetMaxPayloadSize(TransportChannel channel)
        {
            switch (channel)
            {
                case TransportChannel.Reliable:
                    return MaxReliablePayloadSize;
                case TransportChannel.Unreliable:
                    return MaxUnreliablePayloadSize;
                default:
                    throw new ArgumentOutOfRangeException(nameof(channel), channel, "unknown channel");
            }
        }

        public bool TryGetRoundTripTime(ConnectionId connection, out int milliseconds)
        {
            if (_peersById.TryGetValue(connection.Value, out var peer) && peer.Reported && !peer.Closing && peer.Measured)
            {
                milliseconds = (int)Math.Round(peer.RoundTripMilliseconds);
                return true;
            }

            milliseconds = 0;
            return false;
        }

        public bool TryGetProductUserId(ConnectionId connection, out string productUserId)
        {
            if (_peersById.TryGetValue(connection.Value, out var peer) && peer.Reported)
            {
                productUserId = peer.UserId;
                return true;
            }

            productUserId = null;
            return false;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            ReturnLentBuffer();
            if (_pending.Count == 0 && _active)
            {
                Pump();
            }

            // 1 つの相手につなぐだけのクライアントは、切れたら止めて次の Connect を受け付けられるようにする。
            if (_role == Role.Client && _clientEnded)
            {
                StopNetwork();
            }

            if (_pending.Count == 0)
            {
                transportEvent = default;
                return false;
            }

            var next = _pending.Dequeue();
            switch (next.Kind)
            {
                case TransportEventKind.Connected:
                    transportEvent = TransportEvent.Connected(next.Connection);
                    break;
                case TransportEventKind.Disconnected:
                    transportEvent = TransportEvent.Disconnected(next.Connection, next.Reason);
                    break;
                default:
                    _lentBuffer = next.Buffer;
                    transportEvent = TransportEvent.Data(next.Connection, next.Channel, new ArraySegment<byte>(next.Buffer, 0, next.Length));
                    break;
            }

            return true;
        }

        public void Shutdown(string reason)
        {
            var bytes = TransportReason.Encode(reason);
            if (_active)
            {
                if (_signedIn)
                {
                    _scratch.Clear();
                    _scratch.AddRange(_peersById.Values);
                    foreach (var peer in _scratch)
                    {
                        if (peer.Reported && !peer.Closing)
                        {
                            BeginClose(peer, reason ?? "", bytes);
                        }
                    }

                    Linger();
                }

                StopNetwork();
            }

            ReturnLentBuffer();
            while (_pending.Count > 0)
            {
                var dropped = _pending.Dequeue();
                if (dropped.Buffer != null)
                {
                    ArrayPool<byte>.Shared.Return(dropped.Buffer);
                }
            }
        }

        public void Dispose()
        {
            Shutdown(null);
        }

        private static string Seconds(int milliseconds)
        {
            return (milliseconds / 1000.0).ToString("0.#", CultureInfo.InvariantCulture) + "s";
        }

        private static PacketReassembler NewReassembler()
        {
            return new PacketReassembler(_frameHeaderSize + MaxReliablePayloadSize, _maxPendingMessagesPerPeer, _maxPendingBytesPerPeer);
        }

        private void ThrowIfActive()
        {
            if (_active)
            {
                throw new InvalidOperationException("the transport is already " + (_role == Role.Server ? "listening" : "connecting"));
            }
        }

        private void Pump()
        {
            _p2p.Tick();
            DrainNotices();
            if (_active && _signedIn)
            {
                ReceivePackets();
            }

            HandleClosedNotices();

            if (_active)
            {
                RunTimers(Now);
            }
        }

        // 相手が受け取って閉じたと分かるまで、SDK を進めて送りかけの知らせを出し切らせる
        private void Linger()
        {
            _stopping = true;

            // 実際に眠って待つので、差し替えた時計ではなく実際の時間で区切る
            var watch = Stopwatch.StartNew();
            while (AnyPeerClosing() && watch.ElapsedMilliseconds < _options.ShutdownLingerMilliseconds)
            {
                _p2p.Tick();
                DrainNotices();
                HandleClosedNotices();
                if (AnyPeerClosing())
                {
                    Thread.Sleep(_pollMilliseconds);
                }
            }
        }

        private bool AnyPeerClosing()
        {
            foreach (var peer in _peersById.Values)
            {
                if (peer.Closing)
                {
                    return true;
                }
            }

            return false;
        }

        private void DrainNotices()
        {
            while (_active && _p2p.TryTakeNotice(out var notice))
            {
                switch (notice.Kind)
                {
                    case EosP2PNoticeKind.SignedIn:
                        HandleSignedIn(notice.Text);
                        break;
                    case EosP2PNoticeKind.SignInFailed:
                        HandleSignInFailed(notice.Text);
                        break;
                    case EosP2PNoticeKind.ConnectionRequested:
                        HandleConnectionRequested(notice.RemoteUserId);
                        break;
                    case EosP2PNoticeKind.ConnectionEstablished:
                        HandleConnectionEstablished(notice.RemoteUserId);
                        break;
                    default:
                        // 閉じる前に相手が送ったもの (切断の理由の知らせを含む) が受け取りの列に残っていることがあるので、それを読んだ後に閉じる
                        _closedNotices.Add(notice);
                        break;
                }
            }
        }

        private void HandleClosedNotices()
        {
            for (var i = 0; i < _closedNotices.Count && _active; i++)
            {
                HandleConnectionClosed(_closedNotices[i].RemoteUserId, _closedNotices[i].Text);
            }

            _closedNotices.Clear();
        }

        private void HandleSignedIn(string localUserId)
        {
            if (_signedIn)
            {
                return;
            }

            _signedIn = true;
            LocalUserId = localUserId;
            if (_role == Role.Server)
            {
                _log(_logTag + " EOS server ready id=" + localUserId + " socket=" + SocketName + "; connect with "
                    + NetLaunchOptions.ModeFlag + " client "
                    + NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(NetTransportKind.Eos) + " " + NetLaunchOptions.EosServerFlag + " " + localUserId);
                return;
            }

            if (_clientEnded)
            {
                return;
            }

            if (string.Equals(localUserId, _serverUserId, StringComparison.OrdinalIgnoreCase))
            {
                // Device ID のユーザーは端末ごとに 1 つなので、同じ PC・同じ OS ユーザーのサーバーには届かない
                _log(_logTag + " EOS server id is this client's own id; run the server on another machine or OS user");
                EndClient("");
                return;
            }

            _log(_logTag + " EOS logged in id=" + localUserId + "; connecting to " + _serverUserId);

            // 自分から受け入れると、相手に接続の要求が届く
            _p2p.AcceptConnection(_serverUserId);
        }

        private void HandleSignInFailed(string error)
        {
            if (_signedIn)
            {
                return;
            }

            if (_role == Role.Server)
            {
                _signInError = string.IsNullOrEmpty(error) ? "the EOS login failed" : error;
                return;
            }

            _log(_logTag + " " + error);
            EndClient("");
        }

        private void HandleConnectionRequested(string remoteUserId)
        {
            if (_role != Role.Server || !_signedIn || _stopping || remoteUserId.Length == 0)
            {
                return;
            }

            _log(_logTag + " EOS connection request from " + remoteUserId);
            _p2p.AcceptConnection(remoteUserId);
        }

        private void HandleConnectionEstablished(string remoteUserId)
        {
            if (!_signedIn || remoteUserId.Length == 0)
            {
                return;
            }

            if (_role == Role.Client)
            {
                if (string.Equals(remoteUserId, _serverUserId, StringComparison.Ordinal))
                {
                    MarkClientConnected();
                }

                return;
            }

            if (_stopping)
            {
                return;
            }

            // 前の接続が閉じた知らせが同じ tick に残っていれば、新しい接続を前のものと取り違えないよう先に閉じる
            var closed = _closedNotices.FindIndex(n => string.Equals(n.RemoteUserId, remoteUserId, StringComparison.Ordinal));
            if (closed >= 0)
            {
                var notice = _closedNotices[closed];
                _closedNotices.RemoveAt(closed);
                HandleConnectionClosed(notice.RemoteUserId, notice.Text);
            }

            if (_peersByUser.TryGetValue(remoteUserId, out var existing))
            {
                // 切っている最中の相手がつなぎ直してきたら、前の接続は戻ってこないので終えてから新しい接続にする
                if (!existing.Closing)
                {
                    return;
                }

                FinishPeer(existing, existing.LocalReason);
            }

            var peer = AddPeer(remoteUserId);
            peer.Reported = true;
            peer.NextPingAt = Now;
            _pending.Enqueue(Pending.Connected(peer.Id));
            _log(_logTag + " EOS peer connected id=" + peer.Id.Value.ToString(CultureInfo.InvariantCulture) + " user=" + remoteUserId);
        }

        private void HandleConnectionClosed(string remoteUserId, string detail)
        {
            if (!_peersByUser.TryGetValue(remoteUserId, out var peer))
            {
                return;
            }

            var reason = peer.Closing ? peer.LocalReason : "";
            if (_role == Role.Client)
            {
                _log(_logTag + " EOS connection to " + remoteUserId + " ended: closed by EOS: " + detail);
                EndClient(reason);
                return;
            }

            _log(_logTag + " EOS peer closed id=" + peer.Id.Value.ToString(CultureInfo.InvariantCulture) + " reason=" + detail);
            FinishPeer(peer, reason);
        }

        private void ReceivePackets()
        {
            for (var i = 0; i < _maxPacketsPerPump && _active; i++)
            {
                if (!_p2p.TryReceivePacket(_receiveBuffer, out var remoteUserId, out var channel, out var length))
                {
                    return;
                }

                if (length > 0 && length <= MaxPacketSize && remoteUserId != null)
                {
                    HandlePacket(remoteUserId, channel, length);
                }
            }
        }

        private void HandlePacket(string remoteUserId, byte channel, int length)
        {
            // 接続が開いたと知る前・閉じた後に残っていたパケットは、どの接続のものか分からないので捨てる
            if (!_peersByUser.TryGetValue(remoteUserId, out var peer) || peer.Closing)
            {
                return;
            }

            if (_role == Role.Client)
            {
                if (_clientEnded)
                {
                    return;
                }

                MarkClientConnected();
            }
            else if (!peer.Reported)
            {
                return;
            }

            switch (channel)
            {
                case UnreliableChannel:
                    if (length < PacketSplitter.WholeHeaderSize + _frameHeaderSize || _receiveBuffer[0] != PacketSplitter.WholeTag)
                    {
                        Malformed(peer);
                        return;
                    }

                    HandleFrame(peer, TransportChannel.Unreliable, new ArraySegment<byte>(
                        _receiveBuffer, PacketSplitter.WholeHeaderSize, length - PacketSplitter.WholeHeaderSize));
                    return;
                case ReliableChannel:
                    switch (peer.Reassembler.Read(new ArraySegment<byte>(_receiveBuffer, 0, length), out var message))
                    {
                        case PacketReadResult.Complete:
                            if (message.Count < _frameHeaderSize)
                            {
                                Malformed(peer);
                                return;
                            }

                            HandleFrame(peer, TransportChannel.Reliable, message);
                            return;
                        case PacketReadResult.Malformed:
                            Malformed(peer);
                            return;
                        default:
                            return;
                    }

                default:
                    // チャンネル 1 は Netcode の transport の順序なしの送り方、3 は Easy Anti-Cheat のもので、この通信路では使わない
                    return;
            }
        }

        private void HandleFrame(Peer peer, TransportChannel channel, ArraySegment<byte> frame)
        {
            var kind = frame.Array[frame.Offset];
            var body = new ArraySegment<byte>(frame.Array, frame.Offset + _frameHeaderSize, frame.Count - _frameHeaderSize);
            switch (kind)
            {
                case _dataFrame:
                    if (body.Count == 0)
                    {
                        Malformed(peer);
                        return;
                    }

                    var buffer = ArrayPool<byte>.Shared.Rent(body.Count);
                    Buffer.BlockCopy(body.Array, body.Offset, buffer, 0, body.Count);
                    _pending.Enqueue(Pending.Data(peer.Id, channel, buffer, body.Count));
                    return;
                case _closeFrame when channel == TransportChannel.Reliable:
                    ClosedByRemote(peer, TransportReason.Decode(new ReadOnlySpan<byte>(body.Array, body.Offset, body.Count)));
                    return;
                case _pingFrame when channel == TransportChannel.Unreliable && body.Count == _stampSize:
                    SendFrame(peer, _pongFrame, body, false);
                    return;
                case _pongFrame when channel == TransportChannel.Unreliable && body.Count == _stampSize:
                    MeasureRoundTrip(peer, ReadStamp(body));
                    return;
                default:
                    Malformed(peer);
                    return;
            }
        }

        private void ClosedByRemote(Peer peer, string reason)
        {
            if (_role == Role.Client)
            {
                EndClient(reason);
                return;
            }

            _p2p.CloseConnection(peer.UserId);
            FinishPeer(peer, reason);
        }

        private void MeasureRoundTrip(Peer peer, uint stamp)
        {
            var sample = unchecked((uint)Now - stamp);
            if (sample > _maxRoundTripMilliseconds)
            {
                return;
            }

            peer.RoundTripMilliseconds = peer.Measured
                ? peer.RoundTripMilliseconds + ((sample - peer.RoundTripMilliseconds) * _roundTripSmoothing)
                : sample;
            peer.Measured = true;
        }

        private void RunTimers(long now)
        {
            if (_role == Role.Client && !_clientConnected && !_clientEnded && now >= _connectDeadline)
            {
                _log(_logTag + " EOS connection to " + _serverUserId + " timed out after " + Seconds(_options.ConnectTimeoutMilliseconds));
                EndClient("");
                return;
            }

            _scratch.Clear();
            _scratch.AddRange(_peersById.Values);
            foreach (var peer in _scratch)
            {
                if (peer.Closing)
                {
                    if (now >= peer.CloseDeadline)
                    {
                        CloseNow(peer);
                    }
                }
                else if (peer.Reported && now >= peer.NextPingAt)
                {
                    peer.NextPingAt = now + _options.PingIntervalMilliseconds;
                    WriteStamp((uint)now);
                    SendFrame(peer, _pingFrame, new ArraySegment<byte>(_stamp), false);
                }
            }
        }

        private void CloseNow(Peer peer)
        {
            if (_role == Role.Client)
            {
                EndClient(peer.LocalReason);
                return;
            }

            _p2p.CloseConnection(peer.UserId);
            FinishPeer(peer, peer.LocalReason);
        }

        private void BeginClose(Peer peer, string reason, byte[] reasonBytes)
        {
            SendFrame(peer, _closeFrame, new ArraySegment<byte>(reasonBytes), true);
            peer.Closing = true;
            peer.LocalReason = reason;
            peer.CloseDeadline = Now + _options.CloseLingerMilliseconds;
        }

        private void MarkClientConnected()
        {
            if (_clientConnected || _clientEnded || !_peersByUser.TryGetValue(_serverUserId, out var peer))
            {
                return;
            }

            _clientConnected = true;
            peer.Reported = true;
            peer.NextPingAt = Now;
            _pending.Enqueue(Pending.Connected(peer.Id));
            _log(_logTag + " EOS connected to server " + _serverUserId);
        }

        /// <summary>サーバーとの接続を終え、切れた (つながる前ならつながらなかった) と知らせる。</summary>
        private void EndClient(string reason)
        {
            if (_clientEnded)
            {
                return;
            }

            _clientEnded = true;
            if (!_peersByUser.TryGetValue(_serverUserId, out var peer))
            {
                return;
            }

            if (_signedIn)
            {
                _p2p.CloseConnection(_serverUserId);
            }

            RemovePeer(peer);
            _pending.Enqueue(Pending.Disconnected(peer.Id, reason));
        }

        private void FinishPeer(Peer peer, string reason)
        {
            RemovePeer(peer);
            if (peer.Reported)
            {
                _pending.Enqueue(Pending.Disconnected(peer.Id, reason));
            }
        }

        private Peer AddPeer(string userId)
        {
            _lastId++;
            var peer = new Peer(new ConnectionId(_lastId), userId, NewReassembler());
            _peersByUser[userId] = peer;
            _peersById[peer.Id.Value] = peer;
            return peer;
        }

        private void RemovePeer(Peer peer)
        {
            _peersById.Remove(peer.Id.Value);
            if (_peersByUser.TryGetValue(peer.UserId, out var current) && current == peer)
            {
                _peersByUser.Remove(peer.UserId);
            }

            peer.Reassembler.Clear();
        }

        private void StopNetwork()
        {
            if (!_active)
            {
                return;
            }

            _active = false;
            _closedNotices.Clear();
            _p2p.CloseAll();
            _role = Role.None;
            _signedIn = false;
            _stopping = false;
            _signInError = null;
            LocalUserId = null;
            _peersById.Clear();
            _peersByUser.Clear();
        }

        private void SendFrame(Peer peer, byte kind, ArraySegment<byte> body, bool reliable)
        {
            _frame[0] = kind;
            if (body.Count > 0)
            {
                Buffer.BlockCopy(body.Array, body.Offset, _frame, _frameHeaderSize, body.Count);
            }

            _sendTarget = peer.UserId;
            _sendReliable = reliable;
            try
            {
                _splitter.Split(new ArraySegment<byte>(_frame, 0, _frameHeaderSize + body.Count), _sendPacket);
            }
            finally
            {
                _sendTarget = null;
            }
        }

        private void SendPacketToTarget(ArraySegment<byte> packet)
        {
            var channel = _sendReliable ? ReliableChannel : UnreliableChannel;
            if (_p2p.SendPacket(_sendTarget, channel, _sendReliable, packet, out var error))
            {
                return;
            }

            // 送れない状態が続くと tick ごとに出るので、最初と 256 回ごとにだけ出す
            if (_sendFailures++ % 256 == 0)
            {
                _log(_logTag + " EOS SendPacket to " + _sendTarget + " failed: " + error
                    + " (failures=" + _sendFailures.ToString(CultureInfo.InvariantCulture) + ")");
            }
        }

        private void Malformed(Peer peer)
        {
            if (_malformedPackets++ % 256 == 0)
            {
                _log(_logTag + " EOS dropped a malformed packet from id=" + peer.Id.Value.ToString(CultureInfo.InvariantCulture)
                    + " (dropped=" + _malformedPackets.ToString(CultureInfo.InvariantCulture) + ")");
            }
        }

        private void WriteStamp(uint value)
        {
            _stamp[0] = (byte)value;
            _stamp[1] = (byte)(value >> 8);
            _stamp[2] = (byte)(value >> 16);
            _stamp[3] = (byte)(value >> 24);
        }

        private static uint ReadStamp(ArraySegment<byte> body)
        {
            var a = body.Array;
            var o = body.Offset;
            return a[o] | ((uint)a[o + 1] << 8) | ((uint)a[o + 2] << 16) | ((uint)a[o + 3] << 24);
        }

        private void ReturnLentBuffer()
        {
            if (_lentBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_lentBuffer);
                _lentBuffer = null;
            }
        }

        private readonly struct Pending
        {
            private Pending(TransportEventKind kind, ConnectionId connection, TransportChannel channel, byte[] buffer, int length, string reason)
            {
                Kind = kind;
                Connection = connection;
                Channel = channel;
                Buffer = buffer;
                Length = length;
                Reason = reason;
            }

            public TransportEventKind Kind { get; }

            public ConnectionId Connection { get; }

            public TransportChannel Channel { get; }

            public byte[] Buffer { get; }

            public int Length { get; }

            public string Reason { get; }

            public static Pending Connected(ConnectionId connection)
            {
                return new Pending(TransportEventKind.Connected, connection, default, null, 0, null);
            }

            public static Pending Disconnected(ConnectionId connection, string reason)
            {
                return new Pending(TransportEventKind.Disconnected, connection, default, null, 0, reason);
            }

            public static Pending Data(ConnectionId connection, TransportChannel channel, byte[] buffer, int length)
            {
                return new Pending(TransportEventKind.Data, connection, channel, buffer, length, null);
            }
        }

        private sealed class Peer
        {
            public Peer(ConnectionId id, string userId, PacketReassembler reassembler)
            {
                Id = id;
                UserId = userId;
                Reassembler = reassembler;
            }

            public ConnectionId Id { get; }

            public string UserId { get; }

            public PacketReassembler Reassembler { get; }

            /// <summary>つながったと知らせたか。知らせていない相手が消えても、呼ぶ側はその Id を知らないので出さない。</summary>
            public bool Reported { get; set; }

            /// <summary>こちらから切り始めたか。切り始めた後に届いたデータは出さない。</summary>
            public bool Closing { get; set; }

            public string LocalReason { get; set; } = "";

            public long CloseDeadline { get; set; }

            public long NextPingAt { get; set; }

            public bool Measured { get; set; }

            public double RoundTripMilliseconds { get; set; }
        }
    }
}

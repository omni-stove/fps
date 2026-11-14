using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Fps.Core;
using LiteNetLib;

namespace Fps.Protocol
{
    /// <summary>
    /// LiteNetLib の UDP で <see cref="IMatchTransport"/> を実装する。送受信と再送・ping は LiteNetLib のスレッドが行い、
    /// 出来事は <see cref="TryReceive"/> の中で溜めてから返すので、呼ぶ側のスレッドの外で呼び返さない。
    /// </summary>
    public sealed class UdpMatchTransport : IMatchTransport
    {
        /// <summary>合言葉 (<see cref="UdpTransportOptions.ConnectionKey"/>) が違う相手を断るときの理由。</summary>
        public const string ProtocolMismatchReason = DisconnectNotice.RejectedPrefix + "protocol-mismatch";

        /// <summary>LiteNetLib が分けて送るので UDP 1 つに収まらなくてよい。1 つのメッセージの最長 (<see cref="MessageWriter"/>) に合わせる。</summary>
        public const int MaxReliablePayloadSize = MessageWriter.DefaultMaxLength;

        private const int _lingerPollMilliseconds = 5;

        private static readonly byte[] _protocolMismatchBytes = TransportReason.Encode(ProtocolMismatchReason);

        private readonly UdpTransportOptions _options;
        private readonly Listener _listener;
        private readonly Queue<Pending> _pending = new Queue<Pending>();
        private readonly Dictionary<ulong, PeerState> _peers = new Dictionary<ulong, PeerState>();
        private LiteNetManager _manager;
        private Role _role;
        private ulong _lastId;
        private byte[] _lentBuffer;
        private bool _clientEnded;

        public UdpMatchTransport()
            : this(new UdpTransportOptions())
        {
        }

        public UdpMatchTransport(UdpTransportOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _listener = new Listener(this);
        }

        private enum Role
        {
            None,
            Server,
            Client,
        }

        /// <summary>
        /// MTU を探らずに LiteNetLib の初期値のまま使うので、どの経路でも分けずに送れるこの長さで固定できる。
        /// </summary>
        public static int MaxUnreliablePayloadSize => NetConstants.InitialMtu - NetConstants.HeaderSize;

        public bool IsActive => _manager != null;

        /// <summary>待ち受けているかつないでいるローカルのポート。止まっていれば 0。ポート 0 で待ち受けたときに実際のポートを知るのに使う。</summary>
        public int LocalPort => _manager?.LocalPort ?? 0;

        public void Listen(string address, ushort port)
        {
            ThrowIfActive();
            var ip = ParseListenAddress(address);
            var manager = CreateManager();
            if (!manager.Start(ip, IPAddress.IPv6Any, port))
            {
                throw new InvalidOperationException("could not listen on " + ip + ":" + port.ToString(CultureInfo.InvariantCulture));
            }

            _manager = manager;
            _role = Role.Server;
        }

        public void Connect(string address, ushort port)
        {
            ThrowIfActive();
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("server address must not be empty", nameof(address));
            }

            var manager = CreateManager();
            if (!manager.Start())
            {
                throw new InvalidOperationException("could not open a UDP socket to connect from");
            }

            _manager = manager;
            _role = Role.Client;
            _clientEnded = false;

            var id = NextId();
            var ip = ResolveIPv4(address.Trim());
            var peer = ip == null ? null : manager.Connect(new IPEndPoint(ip, port), _options.ConnectionKey);
            if (peer == null)
            {
                _pending.Enqueue(Pending.Disconnected(id, ""));
                _clientEnded = true;
                return;
            }

            var state = new PeerState(id, peer);
            peer.Tag = state;
            _peers.Add(id.Value, state);
        }

        public void Disconnect(ConnectionId connection, string reason)
        {
            var bytes = TransportReason.Encode(reason);
            if (!_peers.TryGetValue(connection.Value, out var state) || state.Closing)
            {
                return;
            }

            state.Closing = true;
            state.LocalReason = reason ?? "";
            _manager.DisconnectPeer(state.Peer, bytes);
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

            if (!_peers.TryGetValue(connection.Value, out var state) || state.Closing)
            {
                return;
            }

            var method = channel == TransportChannel.Reliable ? DeliveryMethod.ReliableOrdered : DeliveryMethod.Unreliable;
            state.Peer.Send(new ReadOnlySpan<byte>(payload.Array, payload.Offset, payload.Count), method);
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
            if (_peers.TryGetValue(connection.Value, out var state) && state.Reported && !state.Closing && state.Measured)
            {
                milliseconds = state.Peer.RoundTripTime;
                return true;
            }

            milliseconds = 0;
            return false;
        }

        public bool TryReceive(out TransportEvent transportEvent)
        {
            ReturnLentBuffer();
            if (_pending.Count == 0 && _manager != null)
            {
                _manager.PollEvents();
            }

            // 1 つの相手につなぐだけのクライアントは、切れたら止めて次の Connect を受け付けられるようにする。
            if (_role == Role.Client && _clientEnded)
            {
                StopManager();
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
            if (_manager != null)
            {
                foreach (var state in _peers.Values)
                {
                    if (!state.Closing)
                    {
                        state.Closing = true;
                        _manager.DisconnectPeer(state.Peer, bytes);
                    }
                }

                Linger();
                StopManager();
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

        private static IPAddress ParseListenAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return IPAddress.Any;
            }

            if (!IPAddress.TryParse(address.Trim(), out var ip) || ip.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new ArgumentException("'" + address + "' is not an IPv4 address to listen on", nameof(address));
            }

            return ip;
        }

        /// <summary>
        /// LiteNetLib の名前解決は OS が IPv6 を使えると IPv6 を先に返すが、ここでは IPv4 だけで開くので IPv4 を選ぶ。
        /// 解決できなければ null を返し、つながらなかった出来事として知らせる。
        /// </summary>
        private static IPAddress ResolveIPv4(string address)
        {
            if (IPAddress.TryParse(address, out var parsed))
            {
                return parsed.AddressFamily == AddressFamily.InterNetwork ? parsed : null;
            }

            try
            {
                foreach (var candidate in Dns.GetHostAddresses(address))
                {
                    if (candidate.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return candidate;
                    }
                }
            }
            catch (SocketException)
            {
            }
            catch (ArgumentException)
            {
            }

            return null;
        }

        private LiteNetManager CreateManager()
        {
            return new LiteNetManager(_listener)
            {
                AutoRecycle = true,
                IPv6Enabled = false,
                DisconnectTimeout = _options.DisconnectTimeoutMilliseconds,
                PingInterval = _options.PingIntervalMilliseconds,
                ReconnectDelay = _options.ConnectAttemptIntervalMilliseconds,
                MaxConnectAttempts = _options.MaxConnectAttempts,
            };
        }

        private void ThrowIfActive()
        {
            if (_manager != null)
            {
                throw new InvalidOperationException("the transport is already " + (_role == Role.Server ? "listening" : "connecting"));
            }
        }

        private ConnectionId NextId()
        {
            _lastId++;
            return new ConnectionId(_lastId);
        }

        private void Linger()
        {
            var watch = Stopwatch.StartNew();
            while (watch.ElapsedMilliseconds < _options.ShutdownLingerMilliseconds && AnyPeerAwaitingShutdown())
            {
                Thread.Sleep(_lingerPollMilliseconds);
            }
        }

        private bool AnyPeerAwaitingShutdown()
        {
            foreach (var state in _peers.Values)
            {
                if (state.Peer.ConnectionState == ConnectionState.ShutdownRequested)
                {
                    return true;
                }
            }

            return false;
        }

        private void StopManager()
        {
            var manager = _manager;
            _manager = null;
            _role = Role.None;
            foreach (var state in _peers.Values)
            {
                state.Peer.Tag = null;
            }

            _peers.Clear();
            manager.Stop();
        }

        private void ReturnLentBuffer()
        {
            if (_lentBuffer != null)
            {
                ArrayPool<byte>.Shared.Return(_lentBuffer);
                _lentBuffer = null;
            }
        }

        private void OnConnectionRequest(LiteConnectionRequest request)
        {
            if (_role != Role.Server)
            {
                request.Reject();
                return;
            }

            if (!request.Data.TryGetString(out var key) || !string.Equals(key, _options.ConnectionKey, StringComparison.Ordinal))
            {
                request.Reject(_protocolMismatchBytes);
                return;
            }

            var peer = request.Accept();
            if (peer == null)
            {
                return;
            }

            var state = new PeerState(NextId(), peer);
            peer.Tag = state;
            _peers.Add(state.Id.Value, state);
        }

        private void OnPeerConnected(LiteNetPeer peer)
        {
            if (!(peer.Tag is PeerState state) || state.Closing)
            {
                return;
            }

            state.Reported = true;
            _pending.Enqueue(Pending.Connected(state.Id));
        }

        private void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo info)
        {
            if (peer == null || !(peer.Tag is PeerState state))
            {
                return;
            }

            peer.Tag = null;
            _peers.Remove(state.Id.Value);
            if (_role == Role.Client)
            {
                _clientEnded = true;
            }
            else if (!state.Reported)
            {
                // 入ってきたと知らせていない相手が消えても、呼ぶ側はその Id を知らないので出さない。
                return;
            }

            string reason;
            if (state.Closing)
            {
                reason = state.LocalReason;
            }
            else if ((info.Reason == DisconnectReason.RemoteConnectionClose || info.Reason == DisconnectReason.ConnectionRejected)
                && info.AdditionalData != null)
            {
                reason = TransportReason.Decode(info.AdditionalData.GetRemainingBytesSpan());
            }
            else
            {
                reason = "";
            }

            _pending.Enqueue(Pending.Disconnected(state.Id, reason));
        }

        private void OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod method)
        {
            if (!(peer.Tag is PeerState state) || !state.Reported || state.Closing)
            {
                return;
            }

            TransportChannel channel;
            switch (method)
            {
                case DeliveryMethod.ReliableOrdered:
                    channel = TransportChannel.Reliable;
                    break;
                case DeliveryMethod.Unreliable:
                    channel = TransportChannel.Unreliable;
                    break;
                default:
                    return;
            }

            var data = reader.GetRemainingBytesSpan();
            if (data.IsEmpty)
            {
                return;
            }

            var buffer = ArrayPool<byte>.Shared.Rent(data.Length);
            data.CopyTo(buffer);
            _pending.Enqueue(Pending.Data(state.Id, channel, buffer, data.Length));
        }

        private static void OnNetworkLatencyUpdate(LiteNetPeer peer)
        {
            if (peer.Tag is PeerState state)
            {
                state.Measured = true;
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

        private sealed class PeerState
        {
            public PeerState(ConnectionId id, LiteNetPeer peer)
            {
                Id = id;
                Peer = peer;
            }

            public ConnectionId Id { get; }

            public LiteNetPeer Peer { get; }

            public bool Reported { get; set; }

            /// <summary>こちらから切り始めたか。切り始めた後に届いたデータは出さない。</summary>
            public bool Closing { get; set; }

            public string LocalReason { get; set; } = "";

            /// <summary>LiteNetLib の往復時間は最初の ping が返るまで 0 なので、測れたかを別に持つ。</summary>
            public bool Measured { get; set; }
        }

        /// <summary>LiteNetLib の型を公開の API に出さないよう、受け口を中に閉じる。</summary>
        private sealed class Listener : ILiteNetEventListener
        {
            private readonly UdpMatchTransport _owner;

            public Listener(UdpMatchTransport owner)
            {
                _owner = owner;
            }

            public void OnConnectionRequest(LiteConnectionRequest request)
            {
                _owner.OnConnectionRequest(request);
            }

            public void OnPeerConnected(LiteNetPeer peer)
            {
                _owner.OnPeerConnected(peer);
            }

            public void OnPeerDisconnected(LiteNetPeer peer, DisconnectInfo disconnectInfo)
            {
                _owner.OnPeerDisconnected(peer, disconnectInfo);
            }

            public void OnNetworkReceive(LiteNetPeer peer, NetPacketReader reader, DeliveryMethod deliveryMethod)
            {
                _owner.OnNetworkReceive(peer, reader, deliveryMethod);
            }

            public void OnNetworkError(IPEndPoint endPoint, SocketError socketError)
            {
            }

            public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
            {
            }

            public void OnNetworkLatencyUpdate(LiteNetPeer peer, int latency)
            {
                UdpMatchTransport.OnNetworkLatencyUpdate(peer);
            }

            public void OnPeerAddressChanged(LiteNetPeer peer, IPEndPoint previousAddress)
            {
            }
        }
    }
}

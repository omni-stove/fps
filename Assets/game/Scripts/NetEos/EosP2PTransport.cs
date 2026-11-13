using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using Fps.Core;
using Unity.Netcode;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// Netcode for GameObjects の transport を EOS の P2P インターフェース (SendPacket / ReceivePacket と
    /// 接続要求の受け入れ) の上に載せたもの。相手は IP アドレスではなく ProductUserId で指定する。
    /// </summary>
    /// <remarks>
    /// PlayEveryWare plugin の P2P Netcode サンプルの transport は plugin の EOSManager (StreamingAssets の設定ファイルで
    /// 認証情報を持つ) を前提にするので取り込まず、NetworkTransport を継承して自作した。
    /// 認証情報は <see cref="EosPlatform"/> が呼び出し側から受け取る。分割・再組み立て・ID の対応づけは Fps.Core 側
    /// (<see cref="PacketSplitter"/> / <see cref="PacketReassembler"/> / <see cref="PeerIdTable"/>) に任せ、
    /// ここは SDK の呼び出しと Netcode のイベントへの変換だけを行う。
    /// Easy Anti-Cheat のパケットは別のチャネルで運び、<see cref="EosAntiCheat"/> に渡す (Netcode には渡さない)。
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class EosP2PTransport : NetworkTransport
    {
        /// <summary>P2P のソケット名。サーバーとクライアントで同じであること (英数字 1〜32 文字)。</summary>
        public const string SocketName = "fps";

        /// <summary>EOS P2P の 1 パケットの上限 (EOS_P2P_MAX_PACKET_SIZE)。</summary>
        public const int MaxPacketSize = 1170;

        /// <summary>受け付けるメッセージの上限。分割して届いてもこれを超えれば捨てる。</summary>
        public const int MaxMessageSize = 1024 * 1024;

        /// <summary>送り手 1 人あたり、組み立て途中に溜めてよい合計バイト数。</summary>
        public const int MaxPendingBytesPerPeer = 2 * 1024 * 1024;

        /// <summary>クライアントがログインからサーバーとつながるまで待つ秒数。</summary>
        public const float ConnectTimeoutSeconds = 20f;

        private const ulong _serverTransportId = 0;
        private const int _maxPendingMessagesPerPeer = 32;
        private const int _maxPacketsPerPump = 4096;
        private const byte _unreliableChannel = 0;
        private const byte _reliableChannel = 1;
        private const byte _orderedChannel = 2;

        /// <summary>Easy Anti-Cheat のパケット (<see cref="AntiCheatPacket"/>) だけを運ぶチャネル。Netcode のデータとは混ぜない。</summary>
        private const byte _antiCheatChannel = 3;

        private readonly Queue<TransportEvent> _events = new Queue<TransportEvent>();
        private readonly PeerIdTable _peers = new PeerIdTable(_serverTransportId + 1);
        private readonly Dictionary<ulong, ProductUserId> _userIds = new Dictionary<ulong, ProductUserId>();
        private readonly Dictionary<ulong, PacketReassembler> _reassemblers = new Dictionary<ulong, PacketReassembler>();
        private readonly PacketSplitter _splitter = new PacketSplitter(MaxPacketSize);
        private readonly byte[] _receiveBuffer = new byte[MaxPacketSize];

        private NetLaunchOptions _options;
        private EosCredentials _credentials;
        private AntiCheatSettings _antiCheatSettings;
        private EosAntiCheat _antiCheat;
        private SessionState _state;
        private bool _isServer;
        private P2PInterface _p2p;
        private EosDeviceLogin _login;
        private ProductUserId _localUserId;
        private ProductUserId _serverUserId;
        private string _serverKey;
        private ulong _requestNotification;
        private ulong _establishedNotification;
        private ulong _closedNotification;
        private bool _clientConnected;
        private bool _clientEnded;
        private float _connectDeadline;
        private int _lastPumpFrame = -1;
        private int _sendFailures;

        private Action<ArraySegment<byte>> _sendPacket;
        private ProductUserId _sendTarget;
        private byte _sendChannel;
        private PacketReliability _sendReliability;

        private enum SessionState
        {
            Idle,
            LoggingIn,
            Ready,
        }

        public override ulong ServerClientId => _serverTransportId;

        /// <summary>ログイン後の自分の ProductUserId。ログイン前は null。</summary>
        public string LocalUserId => _localUserId?.ToString();

        /// <summary>起動前に 1 回呼ぶ。<see cref="EosTransportInstaller"/> が呼ぶ。</summary>
        /// <param name="options">起動設定。transport は EOS であること。</param>
        /// <param name="credentials">EOS の認証情報。</param>
        /// <param name="antiCheat">Easy Anti-Cheat の設定。</param>
        public void Configure(NetLaunchOptions options, EosCredentials credentials, AntiCheatSettings antiCheat)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (options.Transport != NetTransportKind.Eos)
            {
                throw new ArgumentException("options must use the EOS transport", nameof(options));
            }

            if (_state != SessionState.Idle)
            {
                throw new InvalidOperationException("cannot reconfigure a running EOS transport");
            }

            _options = options;
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _antiCheatSettings = antiCheat ?? throw new ArgumentNullException(nameof(antiCheat));
        }

        public override void Initialize(NetworkManager networkManager = null)
        {
            // 送信のたびにデリゲートを作らないよう、1 つを使い回す
            if (_sendPacket == null)
            {
                _sendPacket = SendPacketToTarget;
            }
        }

        public override bool StartServer()
        {
            return Begin(true);
        }

        public override bool StartClient()
        {
            return Begin(false);
        }

        public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
        {
            if (_state != SessionState.Ready)
            {
                return;
            }

            ProductUserId remote;
            if (_isServer)
            {
                if (!_userIds.TryGetValue(clientId, out remote))
                {
                    return;
                }
            }
            else
            {
                if (clientId != _serverTransportId || _clientEnded)
                {
                    return;
                }

                remote = _serverUserId;
            }

            MapDelivery(networkDelivery, out _sendReliability, out _sendChannel);
            _sendTarget = remote;
            try
            {
                _splitter.Split(payload, _sendPacket);
            }
            catch (ArgumentException e)
            {
                Debug.LogError("[Net] EOS cannot send to id=" + clientId + ": " + e.Message);
            }
            finally
            {
                _sendTarget = null;
            }
        }

        public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
        {
            Pump();

            if (_events.Count == 0)
            {
                clientId = 0;
                payload = default;
                receiveTime = Time.realtimeSinceStartup;
                return NetworkEvent.Nothing;
            }

            var next = _events.Dequeue();
            clientId = next.ClientId;
            payload = next.Payload;
            receiveTime = next.ReceiveTime;
            return next.Type;
        }

        public override void DisconnectRemoteClient(ulong clientId)
        {
            if (!_isServer || !_userIds.TryGetValue(clientId, out var remote))
            {
                return;
            }

            ForgetPeer(clientId);
            CloseConnection(remote);
            Debug.Log("[Net] EOS disconnected peer id=" + clientId);
        }

        public override void DisconnectLocalClient()
        {
            if (_isServer)
            {
                return;
            }

            _clientEnded = true;
            if (_serverUserId != null)
            {
                CloseConnection(_serverUserId);
            }
        }

        /// <summary>EOS P2P は往復時間を公開していないので 0 を返す。</summary>
        public override ulong GetCurrentRtt(ulong clientId)
        {
            return 0;
        }

        public override void Shutdown()
        {
            _login?.Cancel();
            _login = null;

            // 相手の登録を外してセッションを終えてから接続を閉じる
            _antiCheat?.End();
            _antiCheat = null;

            if (_p2p != null && _localUserId != null)
            {
                RemoveNotifications();
                var options = new CloseConnectionsOptions
                {
                    LocalUserId = _localUserId,
                    SocketId = Socket(),
                };
                var result = _p2p.CloseConnections(ref options);
                if (result != Result.Success && result != Result.NotFound)
                {
                    Debug.LogWarning("[Net] EOS CloseConnections failed: " + result);
                }
            }

            _events.Clear();
            _peers.Clear();
            _userIds.Clear();
            _reassemblers.Clear();
            _state = SessionState.Idle;
            _p2p = null;
            _localUserId = null;
            _serverUserId = null;
            _serverKey = null;
            _clientConnected = false;
            _clientEnded = false;
            _sendFailures = 0;
        }

        private void Update()
        {
            // Netcode が PollEvent を呼ばない間 (ログイン待ちなど) も SDK を進める
            Pump();
        }

        private void OnDestroy()
        {
            Shutdown();
        }

        private bool Begin(bool isServer)
        {
            if (_options == null || _credentials == null || _antiCheatSettings == null)
            {
                Debug.LogError("[Net] EosP2PTransport.Configure was not called");
                return false;
            }

            if (_state != SessionState.Idle)
            {
                Debug.LogError("[Net] EOS transport is already started");
                return false;
            }

            // SDK の関数 (ProductUserId.FromString を含む) はプラットフォームの初期化後に呼ぶ
            var platform = EosPlatform.Acquire(_credentials, out var error);
            if (platform == null)
            {
                Debug.LogError("[Net] " + error);
                return false;
            }

            _isServer = isServer;
            if (!isServer)
            {
                // EOS が返す ID は小文字なので、大文字で貼られても同じ相手として扱う
                var serverUserId = ProductUserId.FromString(_options.EosServerId.ToLowerInvariant());
                if (serverUserId == null || !serverUserId.IsValid())
                {
                    Debug.LogError("[Net] " + _options.EosServerId + " is not a valid EOS product user id");
                    return false;
                }

                _serverUserId = serverUserId;
                _serverKey = serverUserId.ToString();
                _reassemblers[_serverTransportId] = NewReassembler();
            }

            _p2p = platform.GetP2PInterface();
            var connect = platform.GetConnectInterface();
            if (_p2p == null || connect == null)
            {
                Debug.LogError("[Net] EOS P2P or Connect interface is unavailable");
                _p2p = null;
                return false;
            }

            Initialize();
            _antiCheat = new EosAntiCheat(
                _antiCheatSettings.Enabled, SendAntiCheatPacket, RemoteUserIdOf, RemovePeerForAntiCheat, FailTransport);
            _state = SessionState.LoggingIn;
            _connectDeadline = Time.realtimeSinceStartup + ConnectTimeoutSeconds;

            var role = isServer ? "server" : "client";
            var loggedIn = EosPlatform.LocalUserId;
            if (loggedIn != null)
            {
                // ロビーのために先にログインしていれば、同じユーザーで P2P をつなぐ (ロビーのオーナーの ID が接続先になるため)
                Debug.Log("[Net] EOS reusing the device id login as " + role);
                HandleLoggedIn(loggedIn, null);
                return true;
            }

            _login = new EosDeviceLogin(connect, "fps-" + role, SystemInfo.deviceModel);
            Debug.Log("[Net] EOS logging in with the device id as " + role);
            _login.Begin(HandleLoggedIn);
            return true;
        }

        private void HandleLoggedIn(ProductUserId userId, string error)
        {
            _login = null;
            if (_state != SessionState.LoggingIn)
            {
                return;
            }

            if (error != null)
            {
                FailTransport(error);
                return;
            }

            _localUserId = userId;
            if (!Subscribe())
            {
                FailTransport("EOS P2P notifications could not be registered");
                return;
            }

            // 相手とつながる前に始める。始められなければ (ブートストラッパーを通していないなど) 守られないまま遊ばせない
            if (!_antiCheat.Begin(EosPlatform.Current, userId, out var antiCheatError))
            {
                Debug.LogError(AntiCheatSettings.LogTag + " " + antiCheatError);
                FailTransport("anti-cheat could not start");
                return;
            }

            _state = SessionState.Ready;
            var self = userId.ToString();
            if (_isServer)
            {
                Debug.Log("[Net] EOS server ready id=" + self + " socket=" + SocketName
                    + "; connect with -mode client -transport eos -eos-server " + self);
                return;
            }

            Debug.Log("[Net] EOS logged in id=" + self + "; connecting to " + _serverKey);
            if (_serverKey == self)
            {
                // Device ID のユーザーは端末ごとに 1 つなので、同じ PC・同じ OS ユーザーのサーバーには届かない
                Debug.LogError("[Net] EOS server id is this client's own id; run the server on another machine or OS user");
                EndClient("connecting to self");
                return;
            }

            UseUserIdAsRejoinKey(self);

            // 自分から受け入れると、相手に接続要求が届く
            AcceptConnection(_serverUserId);
        }

        /// <summary>
        /// 接続の承認データの鍵を自分の ProductUserId にする (版はそのまま)。サーバーはこれで、切断して戻ってきた人を見分ける (NetConnectionKeys)。
        /// Netcode は transport がつながった後に承認データを読んで送るので、つなぐ前に差し替えれば間に合う。
        /// </summary>
        private void UseUserIdAsRejoinKey(string userId)
        {
            var manager = GetComponent<NetworkManager>();
            if (manager == null || manager.NetworkConfig == null)
            {
                return;
            }

            if (!RejoinKey.IsValid(userId))
            {
                Debug.LogWarning("[Net] EOS product user id " + userId + " cannot be used as a rejoin key; keeping the device id");
                return;
            }

            if (!ConnectionPayload.TryReplaceRejoinKey(manager.NetworkConfig.ConnectionData, userId, out var payload))
            {
                Debug.LogWarning("[Net] the connection payload was not built by NetworkSetup; keeping it as is");
                return;
            }

            manager.NetworkConfig.ConnectionData = payload;
        }

        private bool Subscribe()
        {
            if (_isServer)
            {
                var requestOptions = new AddNotifyPeerConnectionRequestOptions
                {
                    LocalUserId = _localUserId,
                    SocketId = Socket(),
                };
                _requestNotification = _p2p.AddNotifyPeerConnectionRequest(ref requestOptions, null, HandleConnectionRequest);
            }

            var establishedOptions = new AddNotifyPeerConnectionEstablishedOptions
            {
                LocalUserId = _localUserId,
                SocketId = Socket(),
            };
            _establishedNotification = _p2p.AddNotifyPeerConnectionEstablished(
                ref establishedOptions, null, HandleConnectionEstablished);

            var closedOptions = new AddNotifyPeerConnectionClosedOptions
            {
                LocalUserId = _localUserId,
                SocketId = Socket(),
            };
            _closedNotification = _p2p.AddNotifyPeerConnectionClosed(ref closedOptions, null, HandleConnectionClosed);

            return (!_isServer || _requestNotification != 0) && _establishedNotification != 0 && _closedNotification != 0;
        }

        private void RemoveNotifications()
        {
            if (_requestNotification != 0)
            {
                _p2p.RemoveNotifyPeerConnectionRequest(_requestNotification);
                _requestNotification = 0;
            }

            if (_establishedNotification != 0)
            {
                _p2p.RemoveNotifyPeerConnectionEstablished(_establishedNotification);
                _establishedNotification = 0;
            }

            if (_closedNotification != 0)
            {
                _p2p.RemoveNotifyPeerConnectionClosed(_closedNotification);
                _closedNotification = 0;
            }
        }

        private void HandleConnectionRequest(ref OnIncomingConnectionRequestInfo info)
        {
            if (!_isServer || _state != SessionState.Ready || info.RemoteUserId == null)
            {
                return;
            }

            Debug.Log("[Net] EOS connection request from " + info.RemoteUserId);
            AcceptConnection(info.RemoteUserId);
        }

        private void HandleConnectionEstablished(ref OnPeerConnectionEstablishedInfo info)
        {
            if (_state != SessionState.Ready || info.RemoteUserId == null)
            {
                return;
            }

            if (_isServer)
            {
                RegisterPeer(info.RemoteUserId);
                return;
            }

            if (IsServer(info.RemoteUserId))
            {
                MarkClientConnected();
            }
        }

        private void HandleConnectionClosed(ref OnRemoteConnectionClosedInfo info)
        {
            if (_state != SessionState.Ready || info.RemoteUserId == null)
            {
                return;
            }

            if (_isServer)
            {
                if (_peers.TryGetId(info.RemoteUserId.ToString(), out var id))
                {
                    ForgetPeer(id);
                    Enqueue(NetworkEvent.Disconnect, id, default);
                    Debug.Log("[Net] EOS peer closed id=" + id + " reason=" + info.Reason);
                }

                return;
            }

            if (IsServer(info.RemoteUserId))
            {
                EndClient("closed by EOS: " + info.Reason);
            }
        }

        private void Pump()
        {
            if (_lastPumpFrame == Time.frameCount)
            {
                return;
            }

            _lastPumpFrame = Time.frameCount;
            EosPlatform.Tick();

            if (_state == SessionState.Ready)
            {
                ReceivePackets();
            }

            if (_state == SessionState.Ready)
            {
                _antiCheat?.Tick(Time.realtimeSinceStartupAsDouble);
            }

            if (!_isServer && _state != SessionState.Idle && !_clientConnected && !_clientEnded
                && Time.realtimeSinceStartup > _connectDeadline)
            {
                EndClient("timed out after " + ConnectTimeoutSeconds + "s");
            }
        }

        private void ReceivePackets()
        {
            var options = new ReceivePacketOptions
            {
                LocalUserId = _localUserId,
                MaxDataSizeBytes = MaxPacketSize,
                RequestedChannel = null,
            };
            var buffer = new ArraySegment<byte>(_receiveBuffer);

            for (var i = 0; i < _maxPacketsPerPump && _state == SessionState.Ready; i++)
            {
                ProductUserId peer = null;
                var socket = default(SocketId);
                var result = _p2p.ReceivePacket(ref options, ref peer, ref socket, out var channel, buffer, out var written);
                if (result == Result.NotFound)
                {
                    return;
                }

                if (result != Result.Success)
                {
                    Debug.LogWarning("[Net] EOS ReceivePacket failed: " + result);
                    return;
                }

                HandlePacket(peer, channel, new ArraySegment<byte>(_receiveBuffer, 0, (int)written));
            }
        }

        private void HandlePacket(ProductUserId peer, byte channel, ArraySegment<byte> packet)
        {
            if (peer == null)
            {
                return;
            }

            ulong id;
            if (_isServer)
            {
                // 接続確立の通知より前・切断後に残っていたパケットは、どの接続のものか分からないので捨てる
                if (!_peers.TryGetId(peer.ToString(), out id))
                {
                    return;
                }
            }
            else
            {
                if (_clientEnded || !IsServer(peer))
                {
                    return;
                }

                MarkClientConnected();
                id = _serverTransportId;
            }

            if (channel == _antiCheatChannel)
            {
                // 受信バッファは次のパケットで上書きされるが、EosAntiCheat は渡したその場で使い切るか複製する
                _antiCheat?.HandlePacket(id, packet);
                return;
            }

            if (!_reassemblers.TryGetValue(id, out var reassembler))
            {
                return;
            }

            switch (reassembler.Read(packet, out var message))
            {
                case PacketReadResult.Complete:
                    var copy = new byte[message.Count];
                    Buffer.BlockCopy(message.Array, message.Offset, copy, 0, message.Count);
                    Enqueue(NetworkEvent.Data, id, new ArraySegment<byte>(copy));
                    break;
                case PacketReadResult.Malformed:
                    Debug.LogWarning("[Net] EOS dropped a malformed packet from id=" + id);
                    break;
            }
        }

        private void RegisterPeer(ProductUserId remote)
        {
            var id = _peers.GetOrAdd(remote.ToString(), out var added);
            if (!added)
            {
                return;
            }

            _userIds[id] = remote;
            _reassemblers[id] = NewReassembler();
            Enqueue(NetworkEvent.Connect, id, default);
            Debug.Log("[Net] EOS peer connected id=" + id + " user=" + remote);
            _antiCheat?.PeerConnected(id, Time.realtimeSinceStartupAsDouble);
        }

        private void ForgetPeer(ulong id)
        {
            _peers.Remove(id);
            _userIds.Remove(id);
            _reassemblers.Remove(id);
            _antiCheat?.PeerDisconnected(id);
        }

        private void MarkClientConnected()
        {
            if (_clientConnected || _clientEnded)
            {
                return;
            }

            _clientConnected = true;
            Enqueue(NetworkEvent.Connect, _serverTransportId, default);
            Debug.Log("[Net] EOS connected to server " + _serverKey);
            _antiCheat?.PeerConnected(_serverTransportId, Time.realtimeSinceStartupAsDouble);
        }

        /// <summary>クライアントとしての接続を終え、Netcode に切断 (接続前なら接続失敗) を伝える。</summary>
        private void EndClient(string reason)
        {
            if (_clientEnded)
            {
                return;
            }

            _clientEnded = true;
            Debug.Log("[Net] EOS connection to " + _serverKey + " ended: " + reason);
            _antiCheat?.PeerDisconnected(_serverTransportId);
            if (_p2p != null && _localUserId != null && _serverUserId != null)
            {
                CloseConnection(_serverUserId);
            }

            Enqueue(NetworkEvent.Disconnect, _serverTransportId, default);
        }

        private void FailTransport(string reason)
        {
            Debug.LogError("[Net] " + reason);
            _login?.Cancel();
            _login = null;
            _state = SessionState.Idle;
            Enqueue(NetworkEvent.TransportFailure, _serverTransportId, default);
        }

        private void AcceptConnection(ProductUserId remote)
        {
            var options = new AcceptConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = remote,
                SocketId = Socket(),
            };
            var result = _p2p.AcceptConnection(ref options);
            if (result != Result.Success)
            {
                Debug.LogWarning("[Net] EOS AcceptConnection with " + remote + " failed: " + result);
            }
        }

        private void CloseConnection(ProductUserId remote)
        {
            if (_p2p == null || _localUserId == null)
            {
                return;
            }

            var options = new CloseConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = remote,
                SocketId = Socket(),
            };
            var result = _p2p.CloseConnection(ref options);
            if (result != Result.Success && result != Result.NotFound)
            {
                Debug.LogWarning("[Net] EOS CloseConnection with " + remote + " failed: " + result);
            }
        }

        private void SendPacketToTarget(ArraySegment<byte> packet)
        {
            SendOne(_sendTarget, _sendChannel, _sendReliability, packet);
        }

        private void SendOne(ProductUserId remote, byte channel, PacketReliability reliability, ArraySegment<byte> packet)
        {
            var options = new SendPacketOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = remote,
                SocketId = Socket(),
                Channel = channel,
                Data = packet,
                AllowDelayedDelivery = true,
                Reliability = reliability,
                // 接続は受け入れ・要求の手順でだけ開く。切断した相手への送信で勝手に開き直さない
                DisableAutoAcceptConnection = true,
            };
            var result = _p2p.SendPacket(ref options);
            if (result == Result.Success)
            {
                return;
            }

            // 送れない状態が続くとフレームごとに出るので、最初と 256 回ごとにだけ出す
            if (_sendFailures++ % 256 == 0)
            {
                Debug.LogWarning("[Net] EOS SendPacket to " + remote + " failed: " + result
                    + " (failures=" + _sendFailures + ")");
            }
        }

        /// <summary><see cref="EosAntiCheat"/> のパケットを、EAC 専用のチャネルで信頼性・順序つきで送る。</summary>
        private void SendAntiCheatPacket(ulong id, ArraySegment<byte> packet)
        {
            if (_state != SessionState.Ready)
            {
                return;
            }

            var remote = RemoteUserIdOf(id);
            if (remote == null || (!_isServer && _clientEnded))
            {
                return;
            }

            SendOne(remote, _antiCheatChannel, PacketReliability.ReliableOrdered, packet);
        }

        /// <summary>transport の ID の相手の ProductUserId。接続中の相手でなければ null。</summary>
        private ProductUserId RemoteUserIdOf(ulong id)
        {
            if (_isServer)
            {
                return _userIds.TryGetValue(id, out var remote) ? remote : null;
            }

            return id == _serverTransportId ? _serverUserId : null;
        }

        /// <summary>
        /// EAC が求めた切断。ホストでは相手を切断して Netcode に切断を伝え、クライアントではホストとの接続を終える。
        /// </summary>
        private void RemovePeerForAntiCheat(ulong id, string reason)
        {
            if (!_isServer)
            {
                if (id == _serverTransportId)
                {
                    EndClient("anti-cheat: " + reason);
                }

                return;
            }

            if (!_userIds.TryGetValue(id, out var remote))
            {
                return;
            }

            ForgetPeer(id);
            CloseConnection(remote);
            // Netcode が自分で切ったのではないので、切断を伝えてプレイヤーを片づけさせる
            Enqueue(NetworkEvent.Disconnect, id, default);
            Debug.Log("[Net] EOS removed peer id=" + id + " user=" + remote + " (anti-cheat: " + reason + ")");
        }

        private void Enqueue(NetworkEvent type, ulong clientId, ArraySegment<byte> payload)
        {
            _events.Enqueue(new TransportEvent(type, clientId, payload, Time.realtimeSinceStartup));
        }

        private bool IsServer(ProductUserId remote)
        {
            return _serverKey != null && remote != null && string.Equals(remote.ToString(), _serverKey, StringComparison.Ordinal);
        }

        private static PacketReassembler NewReassembler()
        {
            return new PacketReassembler(MaxMessageSize, _maxPendingMessagesPerPeer, MaxPendingBytesPerPeer);
        }

        private static SocketId Socket()
        {
            return new SocketId { SocketName = SocketName };
        }

        /// <summary>
        /// Netcode の配送方法を EOS の信頼性とチャネルに対応させる。順序つきとそうでないものを別のチャネルにし、
        /// 順序つきのメッセージが順序なしのものを待たないようにする。
        /// </summary>
        private static void MapDelivery(NetworkDelivery delivery, out PacketReliability reliability, out byte channel)
        {
            switch (delivery)
            {
                case NetworkDelivery.Unreliable:
                case NetworkDelivery.UnreliableSequenced:
                    reliability = PacketReliability.UnreliableUnordered;
                    channel = _unreliableChannel;
                    break;
                case NetworkDelivery.Reliable:
                    reliability = PacketReliability.ReliableUnordered;
                    channel = _reliableChannel;
                    break;
                default:
                    reliability = PacketReliability.ReliableOrdered;
                    channel = _orderedChannel;
                    break;
            }
        }

        private readonly struct TransportEvent
        {
            public TransportEvent(NetworkEvent type, ulong clientId, ArraySegment<byte> payload, float receiveTime)
            {
                Type = type;
                ClientId = clientId;
                Payload = payload;
                ReceiveTime = receiveTime;
            }

            public NetworkEvent Type { get; }

            public ulong ClientId { get; }

            public ArraySegment<byte> Payload { get; }

            public float ReceiveTime { get; }
        }
    }
}

using System;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    /// <summary>
    /// クライアントの、サーバーとのやり取り。通信路でつなぎ、承認を頼み、差分のスナップショットを組み立てて受け取ったと知らせ、
    /// サーバーのイベントを渡し、切れた理由を Netcode のクライアントと同じ <see cref="DisconnectNotice"/> に分ける。
    /// </summary>
    /// <remarks>
    /// <para>
    /// 受け取りは呼ぶ側の毎フレームの <see cref="Poll"/> からだけ行い、イベントもその中で呼ぶ。通信路のスレッドから呼び返さないため。
    /// </para>
    /// <para>
    /// 切れた理由は、サーバーが切る前に送るメッセージ (<see cref="ConnectRejectedMessage"/>・<see cref="ServerDisconnectingMessage"/>) の理由を、
    /// 通信路の切断の知らせより先に使う。切断の知らせは UDP 1 つで落ちることがあり、落ちるとタイムアウトに見えるため。
    /// </para>
    /// <para>
    /// <see cref="MessageReceived"/> に渡すメッセージは表 (<see cref="MessageTable"/>) が種類ごとに使い回す入れ物なので、呼び出しの中で読み終える。
    /// </para>
    /// </remarks>
    public sealed class MatchClient : IDisposable
    {
        public const string LogTag = "[Net]";

        private readonly IMatchTransport _transport;
        private readonly Action<string> _log;
        private readonly MessageTable _table = MatchMessages.CreateClientTable();
        private readonly MessageWriter _writer = new MessageWriter();
        private readonly ConnectRequestMessage _request = new ConnectRequestMessage();
        private readonly SnapshotHistory _snapshots = new SnapshotHistory();
        private ConnectionId _server;
        private string _serverReason;
        private bool _leaving;
        private bool _disposed;

        /// <param name="transport">サーバーへつなぐ通信路。このクライアントが持ち、<see cref="Dispose"/> で止める。</param>
        /// <param name="clientVersion">このビルドの正規化した版 (<see cref="BuildVersion"/>)。サーバーは違う版を断る。</param>
        /// <param name="rejoinKey">切れて戻ってきた人を見分ける鍵 (<see cref="RejoinKey"/>)。</param>
        /// <param name="displayName">表示名。</param>
        /// <param name="log">ログの 1 行を受け取る先。<c>[Net]</c> 付きの文を渡す。</param>
        public MatchClient(IMatchTransport transport, string clientVersion, string rejoinKey, string displayName, Action<string> log)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            if (!ConnectionPayload.IsNormalizedVersion(clientVersion))
            {
                throw new ArgumentException("client version '" + clientVersion + "' is not a normalized build version", nameof(clientVersion));
            }

            if (!RejoinKey.IsValid(rejoinKey))
            {
                throw new ArgumentException("rejoin key is not valid", nameof(rejoinKey));
            }

            _request.ClientVersion = clientVersion;
            _request.RejoinKey = rejoinKey;
            _request.DisplayName = displayName ?? "";
            ClientVersion = clientVersion;
        }

        /// <summary>承認された。<see cref="PlayerId"/> などが読める。</summary>
        public event Action Accepted;

        /// <summary>これまでより新しいスナップショットを組み立てた。渡すのは <see cref="LatestSnapshot"/> と同じもの。</summary>
        public event Action<SnapshotMessage> SnapshotReceived;

        /// <summary>承認の後に届いた、スナップショット以外のメッセージ (イベントなど)。</summary>
        public event Action<IMessage> MessageReceived;

        /// <summary>
        /// サーバーとの接続を失った (つながらなかった・断られたを含む)。<see cref="Leave"/> で自分から抜けたときは呼ばない。
        /// </summary>
        public event Action<DisconnectNotice> Disconnected;

        public MatchClientState State { get; private set; } = MatchClientState.Idle;

        public string ClientVersion { get; }

        /// <summary>承認されたこのクライアントのプレイヤーの Id。</summary>
        public int PlayerId { get; private set; }

        public string ServerVersion { get; private set; } = "";

        public int TickRate { get; private set; } = TickClock.DefaultTickRate;

        /// <summary>承認された時点のサーバーの tick。</summary>
        public int AcceptedServerTick { get; private set; }

        /// <summary>サーバーの試合のマップ (シーン名)。</summary>
        public string Map { get; private set; } = "";

        public int MaxHealth { get; private set; } = 1;

        /// <summary>サーバーがプレイヤーを動かす値。予測はこの値で動かす (<see cref="ClientWorld.CreateSimulation"/>)。</summary>
        public NetMotorSettings Motor { get; private set; } = NetMotorSettings.Default;

        /// <summary>サーバーが落ちたプレイヤーを出現地点へ戻す高さ。</summary>
        public float KillPlaneY { get; private set; } = ConnectAcceptedMessage.DefaultKillPlaneY;

        /// <summary>サーバーが吹き飛ばす値。予測はこの値で飛ばす (<see cref="ClientWorld.CreateSimulation"/>)。</summary>
        public KnockbackSpec Knockback { get; private set; } = KnockbackSpec.Default;

        /// <summary>組み立てられた最新のスナップショット。まだ無ければ null。</summary>
        public SnapshotMessage LatestSnapshot => _snapshots.Latest;

        /// <summary>差分の基準が無くて捨てたスナップショットの数。</summary>
        public int UnresolvedSnapshots => _snapshots.Unresolved;

        /// <summary>切れた理由。切れるまでは null。</summary>
        public DisconnectNotice Notice { get; private set; }

        /// <summary>address:port のサーバーへつなぎ始める。</summary>
        /// <exception cref="InvalidOperationException">もうつなぎ始めている。</exception>
        public void Connect(string address, ushort port)
        {
            ThrowIfDisposed();
            if (State != MatchClientState.Idle)
            {
                throw new InvalidOperationException("the client can connect only once (state " + State + ")");
            }

            State = MatchClientState.Connecting;
            _transport.Connect(address, port);
        }

        /// <summary>溜まった出来事を処理する。毎フレーム呼ぶ。</summary>
        public void Poll()
        {
            if (_disposed)
            {
                return;
            }

            while (State != MatchClientState.Disconnected && _transport.TryReceive(out var transportEvent))
            {
                switch (transportEvent.Kind)
                {
                    case TransportEventKind.Connected:
                        HandleConnected(transportEvent.Connection);
                        break;
                    case TransportEventKind.Disconnected:
                        HandleDisconnected(transportEvent.Reason);
                        break;
                    default:
                        HandleData(transportEvent.Payload);
                        break;
                }
            }
        }

        /// <summary>承認された後だけ送る。それ以外では捨てる (サーバーは承認の前のメッセージで切るため)。</summary>
        public void Send(IMessage message, TransportChannel channel)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (State != MatchClientState.Connected)
            {
                return;
            }

            Write(message, channel);
        }

        /// <summary>入力を落ちてよい届け方で送る。組み立てられた最新のスナップショットの tick を差分の基準として載せる。</summary>
        public void SendInput(InputMessage input)
        {
            if (input == null)
            {
                throw new ArgumentNullException(nameof(input));
            }

            if (input.Count == 0)
            {
                return;
            }

            input.AckedSnapshotTick = _snapshots.LatestTick;
            Send(input, TransportChannel.Unreliable);
        }

        public bool TryGetRoundTripTime(out int milliseconds)
        {
            milliseconds = 0;
            return _server.IsValid && !_disposed && _transport.TryGetRoundTripTime(_server, out milliseconds);
        }

        /// <summary>自分から抜ける (メニューへ戻るなど)。<see cref="Disconnected"/> は呼ばない。</summary>
        public void Leave()
        {
            if (_disposed || State == MatchClientState.Disconnected)
            {
                return;
            }

            _leaving = true;
            State = MatchClientState.Disconnected;
            _transport.Shutdown(null);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Leave();
            _disposed = true;
            _transport.Dispose();
        }

        private void HandleConnected(ConnectionId connection)
        {
            if (State != MatchClientState.Connecting)
            {
                return;
            }

            _server = connection;
            State = MatchClientState.Approving;
            Write(_request, TransportChannel.Reliable);
        }

        private void HandleDisconnected(string transportReason)
        {
            var wasConnected = State == MatchClientState.Connected;
            State = MatchClientState.Disconnected;
            if (_leaving)
            {
                return;
            }

            var reason = !string.IsNullOrEmpty(_serverReason) ? _serverReason : transportReason;
            Notice = DisconnectNotice.Classify(reason, wasConnected, ClientVersion);
            Disconnected?.Invoke(Notice);
        }

        private void HandleData(ArraySegment<byte> payload)
        {
            IMessage message;
            try
            {
                message = _table.Read(payload);
            }
            catch (MessageReadException e)
            {
                _log(LogTag + " ignored an unreadable message from the server: " + e.Message);
                return;
            }

            switch (message)
            {
                case ConnectAcceptedMessage accepted:
                    Accept(accepted);
                    return;
                case ConnectRejectedMessage rejected:
                    _serverReason = rejected.Reason;
                    return;
                case ServerDisconnectingMessage disconnecting:
                    _serverReason = disconnecting.Reason;
                    return;
            }

            if (State != MatchClientState.Connected)
            {
                return;
            }

            if (message is SnapshotMessage snapshot)
            {
                var latest = _snapshots.Add(snapshot);
                if (latest != null)
                {
                    SnapshotReceived?.Invoke(latest);
                }

                return;
            }

            MessageReceived?.Invoke(message);
        }

        private void Accept(ConnectAcceptedMessage accepted)
        {
            if (State != MatchClientState.Approving)
            {
                return;
            }

            PlayerId = accepted.PlayerId;
            ServerVersion = accepted.ServerVersion;
            TickRate = accepted.TickRate;
            AcceptedServerTick = accepted.ServerTick;
            Map = accepted.Map;
            MaxHealth = accepted.MaxHealth;
            Motor = accepted.Motor;
            KillPlaneY = accepted.KillPlaneY;
            Knockback = accepted.Knockback;
            State = MatchClientState.Connected;
            _log(LogTag + " client connected id=" + PlayerId.ToString(CultureInfo.InvariantCulture) + " (local) server version="
                + ServerVersion + " map=" + Map + " tickRate=" + TickRate.ToString(CultureInfo.InvariantCulture));
            Accepted?.Invoke();
        }

        private void Write(IMessage message, TransportChannel channel)
        {
            _writer.Reset();
            _writer.WriteMessage(message);
            _transport.Send(_server, channel, _writer.AsSegment());
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(MatchClient));
            }
        }
    }
}

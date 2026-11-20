using System;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // 受け取りは呼ぶ側の毎フレームの Poll からだけ行い、イベントもその中で呼ぶ。通信路のスレッドから呼び返さないため。
    // 切れた理由は、サーバーが切る前に送るメッセージ (ConnectRejectedMessage・ServerDisconnectingMessage) の理由を、
    // 通信路の切断の知らせより先に使う。切断の知らせは UDP 1 つで落ちることがあり、落ちるとタイムアウトに見えるため。
    // MessageReceived に渡すメッセージは表 (MessageTable) が種類ごとに使い回す入れ物なので、受け手は呼び出しの中で読み終える。
    public sealed class MatchClient : IDisposable
    {
        public const string LogTag = "[Net]";

        private readonly IMatchTransport _transport;
        private readonly Action<string> _log;
        private readonly MessageTable _table = MatchMessages.CreateClientTable();
        private readonly MessageWriter _writer = new MessageWriter();
        private readonly ConnectRequestMessage _request = new ConnectRequestMessage();
        private readonly SnapshotHistory _snapshots = new SnapshotHistory();
        private readonly Func<ConnectionExtras> _extras;
        private ConnectionId _server;
        private string _serverReason;
        private bool _leaving;
        private bool _disposed;

        public MatchClient(IMatchTransport transport, string clientVersion, string rejoinKey, string displayName, Action<string> log)
            : this(transport, clientVersion, rejoinKey, displayName, null, log)
        {
        }

        // 値でなく読む関数を受けるのは、EOS の通信路ではログインが済んで通信路がつながるまで自分の ProductUserId が決まらないため。
        public MatchClient(
            IMatchTransport transport, string clientVersion, string rejoinKey, string displayName, Func<ConnectionExtras> extras, Action<string> log)
        {
            _extras = extras;
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

        public event Action Accepted;

        public event Action<SnapshotMessage> SnapshotReceived;

        public event Action<IMessage> MessageReceived;

        public event Action<DisconnectNotice> Disconnected;

        public MatchClientState State { get; private set; } = MatchClientState.Idle;

        public string ClientVersion { get; }

        public int PlayerId { get; private set; }

        public string ServerVersion { get; private set; } = "";

        public int TickRate { get; private set; } = TickClock.DefaultTickRate;

        public int AcceptedServerTick { get; private set; }

        public string Map { get; private set; } = "";

        public int MaxHealth { get; private set; } = 1;

        public NetMotorSettings Motor { get; private set; } = NetMotorSettings.Default;

        public float KillPlaneY { get; private set; } = ConnectAcceptedMessage.DefaultKillPlaneY;

        public KnockbackSpec Knockback { get; private set; } = KnockbackSpec.Default;

        public SnapshotMessage LatestSnapshot => _snapshots.Latest;

        public int UnresolvedSnapshots => _snapshots.Unresolved;

        public DisconnectNotice Notice { get; private set; }

        public ConnectionExtras SentExtras { get; private set; } = ConnectionExtras.None;

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

        // サーバーは承認の前に届いたメッセージで切るので、承認された後だけ送り、それ以外では捨てる。
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

        public bool TryGetLossPercent(out int percent)
        {
            percent = 0;
            return _server.IsValid && !_disposed && _transport is IConnectionLoss loss && loss.TryGetLossPercent(_server, out percent);
        }

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
            var extras = _extras?.Invoke() ?? ConnectionExtras.None;
            _request.PartyId = extras.PartyId ?? "";
            _request.JoinCode = extras.JoinCode ?? "";
            _request.SkillRating = extras.SkillRating;
            _request.ProductUserId = extras.ProductUserId ?? "";
            SentExtras = extras;
            _log(ConnectLog.Requesting(extras));
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

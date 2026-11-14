using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    /// <summary>時刻を呼ぶ側の時計 (秒) で受け取るのは、テストから時間を進められるようにするため。</summary>
    internal sealed class MatchServer : IServerControl
    {
        public const float DefaultApprovalTimeoutSeconds = 10f;
        public const float DrainStatusLogIntervalSeconds = 30f;

        public const string ReconnectedDetail = "reconnected";

        private readonly ServerLaunch _launch;
        private readonly IMatchTransport _transport;
        private readonly ServerHost _host;
        private readonly ServerMapData _map;
        private readonly TextWriter _log;
        private readonly ServerEventWriter _events;
        private readonly Func<DateTime> _utcNow;
        private readonly MessageTable _table = MatchMessages.CreateServerTable();
        private readonly ServerDrain _drain;
        private readonly Dictionary<ConnectionId, float> _pending = new Dictionary<ConnectionId, float>();
        private readonly HashSet<ConnectionId> _closing = new HashSet<ConnectionId>();
        private readonly List<ConnectionId> _expired = new List<ConnectionId>();
        private readonly List<(ConnectionId Connection, string Reason, string LogText)> _kicks =
            new List<(ConnectionId Connection, string Reason, string LogText)>();
        private readonly ServerRulesData _rules;
        private readonly ConnectAcceptedMessage _accepted = new ConnectAcceptedMessage();
        private readonly ConnectRejectedMessage _rejected = new ConnectRejectedMessage();
        private readonly ServerDisconnectingMessage _disconnecting = new ServerDisconnectingMessage();
        private bool _matchEndedWhileDraining;
        private bool _wroteServerStopping;
        private float _nextDrainStatusTime;
        private bool _started;
        private bool _shutDown;

        public MatchServer(
            ServerLaunch launch,
            IMatchTransport transport,
            ServerHost host,
            ServerMapData map,
            TextWriter log,
            ServerEventWriter events,
            Func<DateTime> utcNow,
            float restartDelaySeconds = ServerMatch.DefaultRestartDelaySeconds,
            ServerRulesData rules = null)
        {
            _rules = rules;
            Bodies = new ServerBodies((rules?.Motor ?? NetMotorSettings.Default).Stance);
            _launch = launch ?? throw new ArgumentNullException(nameof(launch));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _events = events ?? throw new ArgumentNullException(nameof(events));
            _utcNow = utcNow ?? throw new ArgumentNullException(nameof(utcNow));
            _drain = new ServerDrain(launch.DrainTimeoutSeconds);
            Match = new ServerMatch(restartDelaySeconds, log, events, utcNow);
            Messenger = new ServerMessenger(transport);
            Match.Ended += _ => _matchEndedWhileDraining |= _drain.IsDraining;
        }

        public ServerMatch Match { get; }

        public ServerMessenger Messenger { get; }

        public ServerBodies Bodies { get; }

        public float ApprovalTimeoutSeconds { get; set; } = DefaultApprovalTimeoutSeconds;

        public bool IsDraining => _drain.IsDraining;

        public bool ExitRequested { get; private set; }

        /// <summary>止まった後にプロセスが返す終了コード。<see cref="Fail"/> されていなければ 0。</summary>
        public int ExitCode { get; private set; } = ServerProgram.ExitOk;

        public int ConnectedPlayers => Match.PlayerCount;

        public bool ListenFailed { get; private set; }

        public void Start()
        {
            if (_started)
            {
                throw new InvalidOperationException("the match server can only be started once");
            }

            _started = true;
            var options = _launch.Options;
            _host.Start(new ServerContext(options, _launch.Version, _log, _map, Match, Messenger, this, _rules, Bodies));
            try
            {
                _transport.Listen(options.Address, options.Port);
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                ListenFailed = true;
                try
                {
                    _host.Stop();
                }
                catch (AggregateException stopFailure)
                {
                    _log.WriteLine(ServerHost.LogTag + " " + stopFailure.Message);
                }

                throw;
            }

            _log.WriteLine("[Net] server listening on " + options.Endpoint);
            _log.WriteLine(_drain.TimeoutSeconds > 0
                ? "[Net] on a stop request (SIGTERM) the server refuses new players and waits up to " + Seconds(_drain.TimeoutSeconds)
                    + " for the match to end; a second request stops it at once"
                : "[Net] on a stop request (SIGTERM) the server stops at once (" + NetLaunchOptions.DrainTimeoutFlag + " 0)");
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.ServerStarted(_utcNow(), _launch.ServerVersion, options.Transport, options.Endpoint));
            }
        }

        public void Tick(float now, float deltaSeconds)
        {
            if (ExitRequested || _shutDown)
            {
                return;
            }

            while (_transport.TryReceive(out var transportEvent))
            {
                Handle(now, transportEvent);
            }

            ExpirePending(now);
            Match.Tick(deltaSeconds);
            _host.Tick(deltaSeconds);
            ApplyKicks();
            TickDrain(now);
        }

        public void RequestStop(float now)
        {
            if (_events.Enabled && !_wroteServerStopping)
            {
                _wroteServerStopping = true;
                _events.Write(ServerEventLog.ServerStopping(_utcNow()));
            }

            var wasDraining = _drain.IsDraining;
            if (_drain.State == ServerDrainState.Finished)
            {
                ExitRequested = true;
                return;
            }

            if (_drain.Request(now, IsMatchOver, ConnectedPlayers))
            {
                _log.WriteLine("[Net] stop requested; draining: refusing new players and waiting up to " + Seconds(_drain.TimeoutSeconds)
                    + " for the match to end (players=" + Count(ConnectedPlayers) + "). Send the stop signal again to stop now");
                _matchEndedWhileDraining = false;
                _nextDrainStatusTime = now + DrainStatusLogIntervalSeconds;
                return;
            }

            _log.WriteLine(wasDraining
                ? "[Net] drain cut short: " + _drain.DescribeStop() + "; stopping now (players=" + Count(ConnectedPlayers) + ")"
                : "[Net] stop requested; stopping now because " + _drain.DescribeStop());
            ExitRequested = true;
        }

        public void Fail(int exitCode, string message)
        {
            if (ExitRequested)
            {
                return;
            }

            _log.WriteLine(message + "; stopping with exit code " + Count(exitCode));
            if (_events.Enabled && !_wroteServerStopping)
            {
                _wroteServerStopping = true;
                _events.Write(ServerEventLog.ServerStopping(_utcNow()));
            }

            ExitCode = exitCode;
            ExitRequested = true;
        }

        public void Shutdown()
        {
            if (_shutDown)
            {
                return;
            }

            _shutDown = true;
            var players = new List<ServerPlayer>(Match.Players);
            _disconnecting.Reason = DisconnectNotice.ServerStoppedReason;
            foreach (var player in players)
            {
                Messenger.Send(player.Connection, TransportChannel.Reliable, _disconnecting);
            }

            foreach (var player in players)
            {
                Match.Leave(player.Connection);
                if (_host.State == ServerHostState.Running)
                {
                    _host.OnDisconnected(player.Connection);
                }
            }

            try
            {
                _host.Stop();
            }
            finally
            {
                _transport.Shutdown(DisconnectNotice.ServerStoppedReason);
                _log.WriteLine("[Net] server stopping; told " + Count(players.Count) + " client(s) and disconnected them");
            }
        }

        // 次の試合までの待ちを 0 秒にしていると、終わった tick のうちに次の試合が始まり、状態を見るだけでは終わりを見逃す
        private bool IsMatchOver => _matchEndedWhileDraining || Match.State == GameModeState.Ended;

        private void TickDrain(float now)
        {
            if (!_drain.IsDraining)
            {
                return;
            }

            if (_drain.Tick(now, IsMatchOver, ConnectedPlayers))
            {
                _log.WriteLine("[Net] drain finished: " + _drain.DescribeStop() + "; stopping with exit code " + Count(ServerProgram.ExitOk));
                ExitRequested = true;
                return;
            }

            if (now >= _nextDrainStatusTime)
            {
                _nextDrainStatusTime = now + DrainStatusLogIntervalSeconds;
                var left = Math.Max(0, (int)Math.Ceiling(_drain.Deadline - now));
                _log.WriteLine("[Net] draining: waiting for the match to end (players=" + Count(ConnectedPlayers) + ", " + Seconds(left) + " left)");
            }
        }

        private void Handle(float now, TransportEvent transportEvent)
        {
            var connection = transportEvent.Connection;
            switch (transportEvent.Kind)
            {
                case TransportEventKind.Connected:
                    _pending[connection] = now;
                    break;
                case TransportEventKind.Disconnected:
                    _pending.Remove(connection);
                    _closing.Remove(connection);
                    if (Match.Leave(connection))
                    {
                        _host.OnDisconnected(connection);
                    }

                    break;
                default:
                    HandleData(connection, transportEvent.Payload);
                    break;
            }
        }

        private void HandleData(ConnectionId connection, ArraySegment<byte> payload)
        {
            if (_closing.Contains(connection))
            {
                return;
            }

            var pending = _pending.ContainsKey(connection);
            IMessage message;
            try
            {
                message = _table.Read(payload);
            }
            catch (MessageReadException e)
            {
                if (pending)
                {
                    Reject(connection, DisconnectNotice.RejectedReason(ConnectionApproval.InvalidPayloadDetail), "unreadable connection request (" + e.Message + ")");
                }
                else
                {
                    Drop(connection, DisconnectNotice.RejectedReason(ConnectionApproval.InvalidPayloadDetail), "unreadable message (" + e.Message + ")");
                }

                return;
            }

            if (pending)
            {
                if (message is ConnectRequestMessage request)
                {
                    Approve(connection, request);
                }
                else
                {
                    Reject(
                        connection,
                        DisconnectNotice.RejectedReason(ConnectionApproval.InvalidPayloadDetail),
                        "sent message " + message.Id.ToString(CultureInfo.InvariantCulture) + " before its connection request");
                }

                return;
            }

            // 承認した後の承認のデータは、届き直したものかもしれないので切らずに捨てる
            if (message is ConnectRequestMessage || !Match.TryGetPlayer(connection, out _))
            {
                return;
            }

            _host.OnMessage(connection, message);
        }

        private void Approve(ConnectionId connection, ConnectRequestMessage request)
        {
            // Netcode の承認データと同じ形にして Core の判定に渡し、版の違い・読めない・drain 中の断り方と文を今のサーバーとそろえる
            var payload = Encoding.UTF8.GetBytes(request.ClientVersion + ConnectionPayload.Separator + request.RejoinKey);
            var verdict = ConnectionApproval.Judge(_launch.ServerVersion, payload, _drain.IsDraining);
            if (!verdict.Approved)
            {
                Reject(connection, verdict.RejectReason, verdict.LogText);
                return;
            }

            if (request.ProtocolVersion != MatchProtocol.Version)
            {
                Reject(
                    connection,
                    UdpMatchTransport.ProtocolMismatchReason,
                    "protocol version mismatch (client " + request.ProtocolVersion.ToString(CultureInfo.InvariantCulture)
                        + ", server " + MatchProtocol.Version.ToString(CultureInfo.InvariantCulture) + ")");
                return;
            }

            if (!ServerMatch.TryGetPlayerId(connection, out var playerId))
            {
                Reject(connection, DisconnectNotice.RejectedReason("server-full"), "connection id does not fit a player id");
                return;
            }

            DropPreviousConnection(verdict.RejoinKey, connection);

            _pending.Remove(connection);
            _accepted.PlayerId = playerId;
            _accepted.ServerVersion = _launch.ServerVersion;
            _accepted.TickRate = TickClock.DefaultTickRate;
            _accepted.ServerTick = (int)Math.Min(_host.TickCount, int.MaxValue);
            Messenger.Send(connection, TransportChannel.Reliable, _accepted);
            _log.WriteLine("[Net] approved client " + connection.Value.ToString(CultureInfo.InvariantCulture) + " (version " + verdict.ClientVersion + ")");

            Match.Join(connection, verdict.RejoinKey);
            _host.OnConnected(connection);
        }

        // 前の接続が切れたと分かる前に同じ人がつなぎ直してきたら、前の接続は戻ってこないので先に抜く
        private void DropPreviousConnection(string rejoinKey, ConnectionId connection)
        {
            var previous = ConnectionId.None;
            foreach (var player in Match.Players)
            {
                if (player.Connection != connection && string.Equals(player.RejoinKey, rejoinKey, StringComparison.Ordinal))
                {
                    previous = player.Connection;
                    break;
                }
            }

            if (previous.IsValid)
            {
                Drop(
                    previous,
                    DisconnectNotice.RejectedReason(ReconnectedDetail),
                    "it reconnected as client " + connection.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        private void Reject(ConnectionId connection, string reason, string logText)
        {
            _log.WriteLine("[Net] rejected client " + connection.Value.ToString(CultureInfo.InvariantCulture) + ": " + logText);
            _pending.Remove(connection);
            _rejected.Reason = reason;
            Close(connection, _rejected, reason);
        }

        private void Drop(ConnectionId connection, string reason, string logText)
        {
            _log.WriteLine("[Net] dropped client " + connection.Value.ToString(CultureInfo.InvariantCulture) + ": " + logText);
            _disconnecting.Reason = reason;
            Close(connection, _disconnecting, reason);
            if (Match.Leave(connection))
            {
                _host.OnDisconnected(connection);
            }
        }

        // 理由はメッセージと切断の知らせの両方に載せる。切断の知らせは UDP 1 つで落ちることがあるため
        private void Close(ConnectionId connection, IMessage notice, string reason)
        {
            Messenger.Send(connection, TransportChannel.Reliable, notice);
            _transport.Disconnect(connection, reason);
            _closing.Add(connection);
        }

        public void Kick(ConnectionId connection, string reason, string logText)
        {
            _kicks.Add((connection, reason, logText));
        }

        private void ApplyKicks()
        {
            // 切ったことで呼ばれるモジュールがさらに切ることもあるので、写してから回す
            while (_kicks.Count > 0)
            {
                var kicks = _kicks.ToArray();
                _kicks.Clear();
                foreach (var kick in kicks)
                {
                    if (Match.TryGetPlayer(kick.Connection, out _))
                    {
                        Drop(kick.Connection, kick.Reason, kick.LogText);
                    }
                }
            }
        }

        private void ExpirePending(float now)
        {
            _expired.Clear();
            foreach (var pair in _pending)
            {
                if (now - pair.Value >= ApprovalTimeoutSeconds)
                {
                    _expired.Add(pair.Key);
                }
            }

            foreach (var connection in _expired)
            {
                Reject(
                    connection,
                    DisconnectNotice.RejectedReason(ConnectionApproval.InvalidPayloadDetail),
                    "no connection request within " + Seconds((int)Math.Ceiling(ApprovalTimeoutSeconds)));
            }
        }

        private static string Seconds(int seconds)
        {
            return seconds.ToString(CultureInfo.InvariantCulture) + "s";
        }

        private static string Count(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

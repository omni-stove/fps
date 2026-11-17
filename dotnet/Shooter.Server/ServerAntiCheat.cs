using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Server.Eos;

namespace Shooter.Server
{
    // Unity の Shooter.NetEos.EosAntiCheat と同じく EAC が無効でも Hello は送り合い、片方だけ有効な組み合わせを同じ理由で切る。
    // 時刻を tick の秒数の和で数えるのは、テストから時間を進められるようにするため。
    internal sealed class ServerAntiCheat : IServerModule, IDisposable
    {
        // Unity の EosAntiCheat.HelloIntervalSeconds とそろえる。
        public const double HelloIntervalSeconds = 1;

        // Unity の EosAntiCheat.HelloTimeoutSeconds とそろえる。
        public const double HelloTimeoutSeconds = 15;

        private const string _tag = AntiCheatSettings.LogTag + " ";

        private readonly AntiCheatSettings _settings;
        private readonly Func<TextWriter, IAntiCheatServerBackend> _createBackend;
        private readonly AntiCheatPeers _peers;
        private readonly AntiCheatToClientMessage _toClient = new AntiCheatToClientMessage();
        private readonly byte[] _sendBuffer = new byte[AntiCheatPacket.MaxPacketSize];
        private readonly Dictionary<ulong, double> _lastHelloAt = new Dictionary<ulong, double>();
        private readonly HashSet<ulong> _warnedUnregistered = new HashSet<ulong>();
        private readonly List<ulong> _helloDue = new List<ulong>();
        private readonly List<ulong> _timedOut = new List<ulong>();
        private ServerContext _context;
        private IAntiCheatServerBackend _backend;
        private double _now;

        public ServerAntiCheat(AntiCheatSettings settings, Func<TextWriter, IAntiCheatServerBackend> createBackend)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (settings.Enabled && createBackend == null)
            {
                throw new ArgumentNullException(nameof(createBackend), "an enabled anti-cheat needs a backend");
            }

            _createBackend = createBackend;
            _peers = new AntiCheatPeers(settings.Enabled, HelloIntervalSeconds, HelloTimeoutSeconds);
        }

        public string Name => "anti-cheat";

        public bool Enabled => _settings.Enabled;

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            if (!Enabled)
            {
                context.Log.WriteLine(_tag + "session not started (disabled); clients are told that anti-cheat is off");
                return;
            }

            // UDP では相手の ProductUserId を確かめられず、EAC に登録できないまま全員を切ることになるので起動させない
            if (context.UserIds == null)
            {
                throw new InvalidOperationException(_tag + "the transport does not know the clients' EOS product user ids; use "
                    + NetLaunchOptions.TransportFlag + " " + NetLaunchOptions.TransportName(NetTransportKind.Eos));
            }

            var backend = _createBackend(context.Log) ?? throw new InvalidOperationException(_tag + "the backend factory returned no backend");
            backend.MessageToClient += HandleMessageToClient;
            backend.ClientActionRequired += HandleClientActionRequired;
            backend.ClientAuthStatusChanged += HandleClientAuthStatusChanged;
            if (!backend.Begin(out var error))
            {
                backend.Dispose();
                throw new InvalidOperationException(_tag + error);
            }

            _backend = backend;
            context.Log.WriteLine(_tag + "session started (client-server)");
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _now += deltaSeconds;
            _backend?.Tick();

            _helloDue.Clear();
            _timedOut.Clear();
            _peers.CollectDue(_now, _helloDue, _timedOut);
            foreach (var peerId in _helloDue)
            {
                SendHello(peerId);
            }

            foreach (var peerId in _timedOut)
            {
                Remove(peerId, _peers.TimeoutReason());
            }
        }

        public void OnConnected(ConnectionId connection)
        {
            if (_peers.Connected(connection.Value, _now))
            {
                SendHello(connection.Value);
            }
        }

        public void OnDisconnected(ConnectionId connection)
        {
            Forget(connection.Value);
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!(message is AntiCheatToServerMessage antiCheat))
            {
                return;
            }

            var peerId = connection.Value;
            var read = AntiCheatPacket.Read(antiCheat.Packet, out var error);
            switch (read.Kind)
            {
                case AntiCheatPacketKind.Hello:
                    HandleHello(peerId, read.PeerEnabled);
                    break;
                case AntiCheatPacketKind.Message:
                    HandleMessage(peerId, read.Payload);
                    break;
                default:
                    WarnOnce(peerId, "dropped a malformed packet from id=" + Id(peerId) + ": " + error);
                    break;
            }
        }

        public void Stop()
        {
            if (_backend != null)
            {
                if (!_backend.End(out var error))
                {
                    _context.Log.WriteLine(_tag + error);
                }
                else
                {
                    _context.Log.WriteLine(_tag + "session ended");
                }
            }

            Dispose();
        }

        public void Dispose()
        {
            _backend?.Dispose();
            _backend = null;
            _peers.Clear();
            _lastHelloAt.Clear();
            _warnedUnregistered.Clear();
        }

        private void HandleHello(ulong peerId, bool remoteEnabled)
        {
            var verdict = _peers.ReceiveHello(peerId, remoteEnabled, out var handle, out var reason);
            switch (verdict)
            {
                case AntiCheatHelloVerdict.Duplicate:
                    // 相手がまだこちらの Hello を受け取っていない (届く前に捨てられた) ので送り直す
                    SendHelloThrottled(peerId);
                    break;
                case AntiCheatHelloVerdict.Accepted:
                    _context.Log.WriteLine(_tag + "peer id=" + Id(peerId) + " also runs without anti-cheat; continuing unprotected");
                    break;
                case AntiCheatHelloVerdict.Registered:
                    RegisterClient(peerId, handle);
                    break;
                case AntiCheatHelloVerdict.Rejected:
                    Remove(peerId, reason);
                    break;
            }
        }

        private void RegisterClient(ulong peerId, long handle)
        {
            if (!_context.UserIds.TryGetProductUserId(new ConnectionId(peerId), out var userId) || string.IsNullOrEmpty(userId))
            {
                Remove(peerId, "its EOS product user id is unknown");
                return;
            }

            if (!_backend.RegisterClient(handle, userId, out var error))
            {
                Remove(peerId, error);
                return;
            }

            _context.Log.WriteLine(_tag + "registered peer id=" + Id(peerId) + " user=" + userId + " handle=" + handle.ToString(CultureInfo.InvariantCulture));

            // Hello より先に届いていたメッセージを、届いた順に EAC へ渡す
            foreach (var early in _peers.TakeEarlyMessages(peerId))
            {
                Forward(peerId, handle, new ArraySegment<byte>(early));
            }
        }

        private void HandleMessage(ulong peerId, ArraySegment<byte> payload)
        {
            if (_peers.TryGetHandle(peerId, out var handle))
            {
                Forward(peerId, handle, payload);
                return;
            }

            // 相手はこちらの Hello を受け取ったのに、こちらは相手の Hello をまだ受け取っていない。取っておき、Hello を送り直させる
            if (_peers.IsAwaitingHello(peerId) && _peers.TryQueueEarlyMessage(peerId, payload))
            {
                SendHelloThrottled(peerId);
                return;
            }

            // 1 つの接続が送り続けてもログを埋めないよう、1 回だけ出す
            WarnOnce(peerId, "dropped a message from id=" + Id(peerId) + " that is not registered with anti-cheat");
        }

        private void Forward(ulong peerId, long handle, ArraySegment<byte> payload)
        {
            if (!_backend.ReceiveMessageFromClient(handle, payload, out var error))
            {
                _context.Log.WriteLine(_tag + "ReceiveMessageFromClient id=" + Id(peerId) + " failed: " + error);
            }
        }

        private void SendHelloThrottled(ulong peerId)
        {
            // 双方が Hello を返し合い続けないよう、直近に送ってから間隔が経つまでは送らない
            if (_lastHelloAt.TryGetValue(peerId, out var last) && _now - last < HelloIntervalSeconds)
            {
                return;
            }

            SendHello(peerId);
        }

        private void SendHello(ulong peerId)
        {
            _lastHelloAt[peerId] = _now;
            var length = AntiCheatPacket.WriteHello(_sendBuffer, Enabled);
            Send(peerId, length);
        }

        private void Send(ulong peerId, int length)
        {
            _toClient.SetPacket(new ArraySegment<byte>(_sendBuffer, 0, length));
            _context.Messenger.Send(new ConnectionId(peerId), TransportChannel.Reliable, _toClient);
        }

        private void Remove(ulong peerId, string reason)
        {
            _context.Log.WriteLine(_tag + "removing peer id=" + Id(peerId) + ": " + reason);
            _context.Control.Kick(new ConnectionId(peerId), DisconnectNotice.RejectedReason(AntiCheatSettings.RejectedDetail), "anti-cheat: " + reason);

            // 切断がモジュールに伝わるのは今の tick の後なので、ここで忘れて登録を外す
            Forget(peerId);
        }

        private void Forget(ulong peerId)
        {
            _lastHelloAt.Remove(peerId);
            _warnedUnregistered.Remove(peerId);
            if (!_peers.Disconnected(peerId, out var handle) || handle == 0 || _backend == null)
            {
                return;
            }

            if (!_backend.UnregisterClient(handle, out var error))
            {
                _context.Log.WriteLine(_tag + "UnregisterClient id=" + Id(peerId) + " failed: " + error);
                return;
            }

            _context.Log.WriteLine(_tag + "unregistered peer id=" + Id(peerId));
        }

        private void WarnOnce(ulong peerId, string text)
        {
            if (_warnedUnregistered.Add(peerId))
            {
                _context.Log.WriteLine(_tag + text);
            }
        }

        private void HandleMessageToClient(long handle, ArraySegment<byte> message)
        {
            if (!_peers.TryGetPeer(handle, out var peerId))
            {
                return;
            }

            int length;
            try
            {
                length = AntiCheatPacket.WriteMessage(_sendBuffer, message);
            }
            catch (ArgumentException e)
            {
                _context.Log.WriteLine(_tag + "cannot relay a message to id=" + Id(peerId) + ": " + e.Message);
                return;
            }

            Send(peerId, length);
        }

        private void HandleClientActionRequired(long handle, bool removePlayer, string description)
        {
            if (!_peers.TryGetPeer(handle, out var peerId))
            {
                return;
            }

            if (!removePlayer)
            {
                _context.Log.WriteLine(_tag + "ignoring action " + description + " for id=" + Id(peerId));
                return;
            }

            Remove(peerId, "anti-cheat violation " + description);
        }

        private void HandleClientAuthStatusChanged(long handle, string status)
        {
            if (_peers.TryGetPeer(handle, out var peerId))
            {
                _context.Log.WriteLine(_tag + "peer id=" + Id(peerId) + " auth status=" + status);
            }
        }

        private static string Id(ulong peerId)
        {
            return peerId.ToString(CultureInfo.InvariantCulture);
        }
    }
}

using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    // EAC が無効でも Hello を送り、片方だけ有効ならサーバーと同じ理由で抜ける。
    // サーバーをピアとして登録しないのは、ClientServer モードの EAC ではクライアントがサーバーを登録せず、
    // メッセージを受け渡すだけだから。セッションを承認の後に始めるのは、サーバーが承認の前に届いたメッセージで切るため。
    public sealed class ClientAntiCheat
    {
        public const double HelloIntervalSeconds = 1;

        public const double HelloTimeoutSeconds = 15;

        // EOS の P2P のクライアントがホストを指していた Id とそろえ、手順書のログと同じにする。
        public const ulong ServerPeerId = 0;

        private const string _tag = AntiCheatSettings.LogTag + " ";

        private readonly IAntiCheatClientSession _session;
        private readonly Action<IMessage> _send;
        private readonly Action<string> _log;
        private readonly Action<string> _remove;
        private readonly AntiCheatPeers _peers;
        private readonly AntiCheatToServerMessage _toServer = new AntiCheatToServerMessage();
        private readonly byte[] _sendBuffer = new byte[AntiCheatPacket.MaxPacketSize];
        private readonly List<ulong> _helloDue = new List<ulong>();
        private readonly List<ulong> _timedOut = new List<ulong>();
        private double _lastHelloAt = double.NegativeInfinity;
        private double _now;
        private bool _sessionStarted;
        private bool _removed;
        private string _pendingRemoval;
        private double _removeAt;

        public ClientAntiCheat(bool enabled, IAntiCheatClientSession session, Action<IMessage> send, Action<string> log, Action<string> remove)
        {
            if (enabled && session == null)
            {
                throw new ArgumentNullException(nameof(session), "an enabled anti-cheat needs a session");
            }

            _session = session;
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _remove = remove ?? throw new ArgumentNullException(nameof(remove));
            _peers = new AntiCheatPeers(enabled, HelloIntervalSeconds, HelloTimeoutSeconds);
        }

        public bool Enabled => _peers.LocalEnabled;

        public bool Removed => _removed;

        // 始められなければ false と理由を返し、呼ぶ側は守られないまま遊ばせずに抜ける。
        public bool ServerConnected(double now, out string error)
        {
            _now = now;
            error = null;
            if (_removed || !_peers.Connected(ServerPeerId, now))
            {
                return true;
            }

            if (!Enabled)
            {
                _log(_tag + "session not started (disabled); the server is told that anti-cheat is off");
            }
            else if (!_sessionStarted)
            {
                if (!_session.BeginSession(SendMessageToServer, Fail, out error))
                {
                    _peers.Clear();
                    return false;
                }

                _sessionStarted = true;
                _log(_tag + "session started (client-server)");
            }

            SendHello();
            return true;
        }

        public void HandleMessage(AntiCheatToClientMessage message, double now)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            _now = now;
            if (_removed || _pendingRemoval != null)
            {
                return;
            }

            var read = AntiCheatPacket.Read(message.Packet, out var error);
            switch (read.Kind)
            {
                case AntiCheatPacketKind.Hello:
                    HandleHello(read.PeerEnabled);
                    break;
                case AntiCheatPacketKind.Message:
                    HandleServerMessage(read.Payload);
                    break;
                default:
                    _log(_tag + "dropped a malformed packet from id=" + ServerPeerId + ": " + error);
                    break;
            }
        }

        public void Tick(double now)
        {
            _now = now;
            if (_removed)
            {
                return;
            }

            if (_pendingRemoval != null)
            {
                if (now >= _removeAt)
                {
                    Leave(_pendingRemoval);
                }

                return;
            }

            _helloDue.Clear();
            _timedOut.Clear();
            _peers.CollectDue(now, _helloDue, _timedOut);
            if (_helloDue.Count > 0)
            {
                SendHello();
            }

            if (_timedOut.Count > 0)
            {
                Remove(_peers.TimeoutReason());
            }
        }

        public void End()
        {
            if (_sessionStarted)
            {
                _sessionStarted = false;
                _session.EndSession();
                _log(_tag + "session ended");
            }

            _pendingRemoval = null;
            _peers.Clear();
        }

        private void HandleHello(bool serverEnabled)
        {
            var verdict = _peers.ReceiveHello(ServerPeerId, serverEnabled, out _, out var reason);
            switch (verdict)
            {
                case AntiCheatHelloVerdict.Duplicate:
                    // サーバーがまだこちらの Hello を受け取っていない (届く前に捨てられた) ので送り直す
                    SendHelloThrottled();
                    break;
                case AntiCheatHelloVerdict.Accepted:
                    _log(_tag + "server id=" + ServerPeerId + " also runs without anti-cheat; continuing unprotected");
                    break;
                case AntiCheatHelloVerdict.Registered:
                    _log(_tag + "server id=" + ServerPeerId + " runs anti-cheat; relaying its messages");
                    foreach (var early in _peers.TakeEarlyMessages(ServerPeerId))
                    {
                        Forward(new ArraySegment<byte>(early));
                    }

                    break;
                case AntiCheatHelloVerdict.Rejected:
                    // サーバーも同じ食い違いで切るので、こちらの Hello がサーバーへ届くまで抜けるのを待つ。すぐ通信路を止めると
                    // 送ったばかりの Hello が届かず、サーバーのログに理由が残らないため
                    _log(_tag + "removing peer id=" + ServerPeerId + ": " + reason);
                    _pendingRemoval = reason;
                    _removeAt = _now + HelloIntervalSeconds;
                    break;
            }
        }

        private void HandleServerMessage(ArraySegment<byte> payload)
        {
            if (_peers.TryGetHandle(ServerPeerId, out _))
            {
                Forward(payload);
                return;
            }

            // サーバーはこちらの Hello を受け取ったのに、こちらはサーバーの Hello をまだ受け取っていない。取っておき、Hello を送り直させる
            if (_peers.IsAwaitingHello(ServerPeerId) && _peers.TryQueueEarlyMessage(ServerPeerId, payload))
            {
                SendHelloThrottled();
                return;
            }

            _log(_tag + "dropped a message from id=" + ServerPeerId + " that is not registered with anti-cheat");
        }

        private void Forward(ArraySegment<byte> payload)
        {
            if (_sessionStarted && !_session.ReceiveMessageFromServer(payload, out var error))
            {
                _log(_tag + "ReceiveMessageFromServer failed: " + error);
            }
        }

        private void SendMessageToServer(ArraySegment<byte> message)
        {
            if (_removed || !_sessionStarted)
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
                _log(_tag + "cannot relay a message to id=" + ServerPeerId + ": " + e.Message);
                return;
            }

            Send(length);
        }

        private void SendHelloThrottled()
        {
            // 双方が Hello を返し合い続けないよう、直近に送ってから間隔が経つまでは送らない
            if (_now - _lastHelloAt < HelloIntervalSeconds)
            {
                return;
            }

            SendHello();
        }

        private void SendHello()
        {
            _lastHelloAt = _now;
            Send(AntiCheatPacket.WriteHello(_sendBuffer, Enabled));
        }

        private void Send(int length)
        {
            _toServer.SetPacket(new ArraySegment<byte>(_sendBuffer, 0, length));
            _send(_toServer);
        }

        private void Fail(string reason)
        {
            _log(_tag + reason);
            Remove("anti-cheat: " + reason);
        }

        private void Remove(string reason)
        {
            if (_removed)
            {
                return;
            }

            _log(_tag + "removing peer id=" + ServerPeerId + ": " + reason);
            Leave(reason);
        }

        private void Leave(string reason)
        {
            _removed = true;
            _pendingRemoval = null;
            End();
            _remove(reason);
        }
    }
}

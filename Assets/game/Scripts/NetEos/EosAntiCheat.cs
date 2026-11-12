using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.AntiCheatClient;
using Epic.OnlineServices.AntiCheatCommon;
using Epic.OnlineServices.Platform;
using Fps.Core;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// Easy Anti-Cheat の Anti-Cheat Client インターフェースを PeerToPeer モードで使い、<see cref="EosP2PTransport"/> の
    /// 相手をピアとして登録する。ホスト型なので、ホストは接続してきたクライアント全員を、クライアントはホストだけを登録する
    /// (クライアント同士はホストを通してしかやりとりしない)。EAC が相手へ送るよう求めたメッセージは transport の
    /// 専用チャネルで中継し、相手から届いたものを EAC へ渡す。EAC が相手の削除を求めたら transport に切断させる。
    /// </summary>
    /// <remarks>
    /// 相手ごとの状態と判断 (Hello のやりとり・登録・拒否・期限切れ) は <see cref="AntiCheatPeers"/> に任せ、
    /// ここは SDK の呼び出しとログだけを行う。EAC が無効でも Hello は送り、片方だけ有効な組み合わせを切断する。
    /// </remarks>
    internal sealed class EosAntiCheat
    {
        /// <summary>相手の Hello が来るまで、こちらの Hello を送り直す間隔 (秒)。</summary>
        public const double HelloIntervalSeconds = 1;

        /// <summary>接続から相手の Hello を待つ秒数。</summary>
        public const double HelloTimeoutSeconds = 15;

        /// <summary>EAC が相手の認証を待つ秒数 (SDK が受け付けるのは 40〜120)。</summary>
        public const uint AuthenticationTimeoutSeconds = 60;

        private const string _tag = AntiCheatSettings.LogTag + " ";

        private readonly AntiCheatPeers _peers;
        private readonly Action<ulong, ArraySegment<byte>> _send;
        private readonly Func<ulong, ProductUserId> _userIdOf;
        private readonly Action<ulong, string> _remove;
        private readonly Action<string> _fail;
        private readonly byte[] _sendBuffer = new byte[AntiCheatPacket.MaxPacketSize];
        private readonly List<ulong> _helloDue = new List<ulong>();
        private readonly List<ulong> _timedOut = new List<ulong>();

        private AntiCheatClientInterface _client;
        private bool _sessionStarted;
        private ulong _messageNotification;
        private ulong _actionNotification;
        private ulong _authNotification;
        private ulong _violationNotification;

        /// <param name="enabled">こちらの EAC を有効にするか。</param>
        /// <param name="send">相手へ EAC 用のパケットを信頼性・順序つきで送る。</param>
        /// <param name="userIdOf">相手の ProductUserId を返す。分からなければ null。</param>
        /// <param name="remove">相手を切断する (理由つき)。切断した相手は <see cref="PeerDisconnected"/> で伝えること。</param>
        /// <param name="fail">こちら側で EAC を続けられなくなったときに transport を止める (理由つき)。</param>
        public EosAntiCheat(
            bool enabled,
            Action<ulong, ArraySegment<byte>> send,
            Func<ulong, ProductUserId> userIdOf,
            Action<ulong, string> remove,
            Action<string> fail)
        {
            _send = send ?? throw new ArgumentNullException(nameof(send));
            _userIdOf = userIdOf ?? throw new ArgumentNullException(nameof(userIdOf));
            _remove = remove ?? throw new ArgumentNullException(nameof(remove));
            _fail = fail ?? throw new ArgumentNullException(nameof(fail));
            _peers = new AntiCheatPeers(enabled, HelloIntervalSeconds, HelloTimeoutSeconds);
        }

        public bool Enabled => _peers.LocalEnabled;

        /// <summary>
        /// ログイン後に呼ぶ。EAC が有効ならセッションを始める。無効なら何もせず true。
        /// </summary>
        /// <returns>始められなければ false と理由 (ブートストラッパーを通さずに起動した、など)。</returns>
        public bool Begin(PlatformInterface platform, ProductUserId localUserId, out string error)
        {
            if (platform == null)
            {
                throw new ArgumentNullException(nameof(platform));
            }

            if (localUserId == null)
            {
                throw new ArgumentNullException(nameof(localUserId));
            }

            error = null;
            if (!Enabled)
            {
                Debug.Log(_tag + "session not started (disabled); peers are told that anti-cheat is off");
                return true;
            }

            if (_sessionStarted)
            {
                return true;
            }

            _client = platform.GetAntiCheatClientInterface();
            if (_client == null)
            {
                error = "the EOS anti-cheat client interface is unavailable" + LaunchHint();
                return false;
            }

            if (!Subscribe())
            {
                RemoveNotifications();
                _client = null;
                error = "anti-cheat notifications could not be registered" + LaunchHint();
                return false;
            }

            var options = new BeginSessionOptions
            {
                LocalUserId = localUserId,
                Mode = AntiCheatClientMode.PeerToPeer,
            };
            var result = _client.BeginSession(ref options);
            if (result != Result.Success)
            {
                RemoveNotifications();
                _client = null;
                error = "anti-cheat BeginSession failed: " + result + LaunchHint();
                return false;
            }

            _sessionStarted = true;
            Debug.Log(_tag + "session started (peer-to-peer) user=" + localUserId);
            return true;
        }

        /// <summary>相手との接続ができた。すぐにこちらの Hello を送る。</summary>
        public void PeerConnected(ulong peerId, double now)
        {
            if (_peers.Connected(peerId, now))
            {
                SendHello(peerId);
            }
        }

        /// <summary>相手との接続が切れた (こちらから切った場合も含む)。EAC に登録していれば外す。</summary>
        public void PeerDisconnected(ulong peerId)
        {
            if (!_peers.Disconnected(peerId, out var handle) || handle == 0)
            {
                return;
            }

            if (_client == null || !_sessionStarted)
            {
                return;
            }

            var options = new UnregisterPeerOptions { PeerHandle = new IntPtr(handle) };
            var result = _client.UnregisterPeer(ref options);
            if (result != Result.Success)
            {
                Debug.LogWarning(_tag + "UnregisterPeer id=" + peerId + " failed: " + result);
                return;
            }

            Debug.Log(_tag + "unregistered peer id=" + peerId);
        }

        /// <summary>相手から EAC 用のチャネルでパケットが届いた。</summary>
        public void HandlePacket(ulong peerId, ArraySegment<byte> packet)
        {
            var read = AntiCheatPacket.Read(packet, out var error);
            switch (read.Kind)
            {
                case AntiCheatPacketKind.Hello:
                    HandleHello(peerId, read.PeerEnabled);
                    break;
                case AntiCheatPacketKind.Message:
                    HandleMessage(peerId, read.Payload);
                    break;
                default:
                    Debug.LogWarning(_tag + "dropped a malformed packet from id=" + peerId + ": " + error);
                    break;
            }
        }

        /// <summary>毎フレーム呼ぶ。Hello の送り直しと、Hello が来ない相手の切断をする。</summary>
        public void Tick(double now)
        {
            _helloDue.Clear();
            _timedOut.Clear();
            _peers.CollectDue(now, _helloDue, _timedOut);

            foreach (var peerId in _helloDue)
            {
                SendHello(peerId);
            }

            foreach (var peerId in _timedOut)
            {
                Remove(peerId, _peers.TimeoutReason());
            }
        }

        /// <summary>セッションを終え、通知を外し、相手をすべて忘れる。transport を止めるときに呼ぶ。</summary>
        public void End()
        {
            if (_client != null)
            {
                RemoveNotifications();
                if (_sessionStarted)
                {
                    var options = new EndSessionOptions();
                    var result = _client.EndSession(ref options);
                    if (result != Result.Success)
                    {
                        Debug.LogWarning(_tag + "EndSession failed: " + result);
                    }
                    else
                    {
                        Debug.Log(_tag + "session ended");
                    }
                }
            }

            _sessionStarted = false;
            _client = null;
            _peers.Clear();
        }

        private void HandleHello(ulong peerId, bool remoteEnabled)
        {
            var verdict = _peers.ReceiveHello(peerId, remoteEnabled, out var handle, out var reason);
            switch (verdict)
            {
                case AntiCheatHelloVerdict.Duplicate:
                    // 相手がまだこちらの Hello を受け取っていない (届く前に捨てられた) ので送り直す
                    SendHello(peerId);
                    break;
                case AntiCheatHelloVerdict.Accepted:
                    Debug.Log(_tag + "peer id=" + peerId + " also runs without anti-cheat; continuing unprotected");
                    break;
                case AntiCheatHelloVerdict.Registered:
                    RegisterPeer(peerId, handle);
                    break;
                case AntiCheatHelloVerdict.Rejected:
                    Remove(peerId, reason);
                    break;
            }
        }

        private void RegisterPeer(ulong peerId, long handle)
        {
            var remote = _userIdOf(peerId);
            if (remote == null || !remote.IsValid())
            {
                Remove(peerId, "its EOS product user id is unknown");
                return;
            }

            if (_client == null || !_sessionStarted)
            {
                Remove(peerId, "the local anti-cheat session is not running");
                return;
            }

            var options = new RegisterPeerOptions
            {
                PeerHandle = new IntPtr(handle),
                ClientType = AntiCheatCommonClientType.ProtectedClient,
                ClientPlatform = AntiCheatCommonClientPlatform.Unknown,
                AuthenticationTimeout = AuthenticationTimeoutSeconds,
                PeerProductUserId = remote,
                IpAddress = null,
            };
            var result = _client.RegisterPeer(ref options);
            if (result != Result.Success)
            {
                Remove(peerId, "RegisterPeer failed: " + result);
                return;
            }

            Debug.Log(_tag + "registered peer id=" + peerId + " user=" + remote + " handle=" + handle);

            // Hello より先に届いていたメッセージを、届いた順に EAC へ渡す
            foreach (var early in _peers.TakeEarlyMessages(peerId))
            {
                ForwardToClient(peerId, handle, new ArraySegment<byte>(early));
            }
        }

        private void HandleMessage(ulong peerId, ArraySegment<byte> payload)
        {
            if (_peers.TryGetHandle(peerId, out var handle))
            {
                ForwardToClient(peerId, handle, payload);
                return;
            }

            if (_peers.IsAwaitingHello(peerId))
            {
                // 相手はこちらを登録済みなのに、こちらは相手の Hello を受け取っていない (届く前に捨てられた)。
                // メッセージは取っておき、こちらの Hello を送って相手に Hello を送り直させる
                if (_peers.TryQueueEarlyMessage(peerId, payload))
                {
                    SendHello(peerId);
                    return;
                }
            }

            Debug.LogWarning(_tag + "dropped a message from id=" + peerId + " that is not registered with anti-cheat");
        }

        private void ForwardToClient(ulong peerId, long handle, ArraySegment<byte> payload)
        {
            if (_client == null || !_sessionStarted)
            {
                return;
            }

            var options = new ReceiveMessageFromPeerOptions
            {
                PeerHandle = new IntPtr(handle),
                Data = payload,
            };
            var result = _client.ReceiveMessageFromPeer(ref options);
            if (result != Result.Success)
            {
                Debug.LogWarning(_tag + "ReceiveMessageFromPeer id=" + peerId + " failed: " + result);
            }
        }

        private void SendHello(ulong peerId)
        {
            var length = AntiCheatPacket.WriteHello(_sendBuffer, Enabled);
            _send(peerId, new ArraySegment<byte>(_sendBuffer, 0, length));
        }

        private void Remove(ulong peerId, string reason)
        {
            Debug.LogWarning(_tag + "removing peer id=" + peerId + ": " + reason);
            _remove(peerId, reason);
            // transport が切断を伝えてこなかった場合も、ここで忘れて登録を外す
            PeerDisconnected(peerId);
        }

        private bool Subscribe()
        {
            var messageOptions = new AddNotifyMessageToPeerOptions();
            _messageNotification = _client.AddNotifyMessageToPeer(ref messageOptions, null, HandleMessageToPeer);

            var actionOptions = new AddNotifyPeerActionRequiredOptions();
            _actionNotification = _client.AddNotifyPeerActionRequired(ref actionOptions, null, HandlePeerActionRequired);

            var authOptions = new AddNotifyPeerAuthStatusChangedOptions();
            _authNotification = _client.AddNotifyPeerAuthStatusChanged(ref authOptions, null, HandlePeerAuthStatusChanged);

            var violationOptions = new AddNotifyClientIntegrityViolatedOptions();
            _violationNotification = _client.AddNotifyClientIntegrityViolated(
                ref violationOptions, null, HandleClientIntegrityViolated);

            return _messageNotification != 0 && _actionNotification != 0 && _authNotification != 0
                && _violationNotification != 0;
        }

        private void RemoveNotifications()
        {
            if (_client == null)
            {
                return;
            }

            if (_messageNotification != 0)
            {
                _client.RemoveNotifyMessageToPeer(_messageNotification);
                _messageNotification = 0;
            }

            if (_actionNotification != 0)
            {
                _client.RemoveNotifyPeerActionRequired(_actionNotification);
                _actionNotification = 0;
            }

            if (_authNotification != 0)
            {
                _client.RemoveNotifyPeerAuthStatusChanged(_authNotification);
                _authNotification = 0;
            }

            if (_violationNotification != 0)
            {
                _client.RemoveNotifyClientIntegrityViolated(_violationNotification);
                _violationNotification = 0;
            }
        }

        private void HandleMessageToPeer(ref OnMessageToClientCallbackInfo info)
        {
            if (!_peers.TryGetPeer(info.ClientHandle.ToInt64(), out var peerId))
            {
                return;
            }

            int length;
            try
            {
                length = AntiCheatPacket.WriteMessage(_sendBuffer, info.MessageData);
            }
            catch (ArgumentException e)
            {
                Debug.LogError(_tag + "cannot relay a message to id=" + peerId + ": " + e.Message);
                return;
            }

            _send(peerId, new ArraySegment<byte>(_sendBuffer, 0, length));
        }

        private void HandlePeerActionRequired(ref OnClientActionRequiredCallbackInfo info)
        {
            if (!_peers.TryGetPeer(info.ClientHandle.ToInt64(), out var peerId))
            {
                return;
            }

            var detailsText = info.ActionReasonDetailsString?.ToString();
            var details = string.IsNullOrEmpty(detailsText) ? "" : " (" + detailsText + ")";
            if (info.ClientAction != AntiCheatCommonClientAction.RemovePlayer)
            {
                Debug.LogWarning(_tag + "ignoring action " + info.ClientAction + " for id=" + peerId
                    + " reason=" + info.ActionReasonCode + details);
                return;
            }

            Remove(peerId, "anti-cheat violation " + info.ActionReasonCode + details);
        }

        private void HandlePeerAuthStatusChanged(ref OnClientAuthStatusChangedCallbackInfo info)
        {
            if (_peers.TryGetPeer(info.ClientHandle.ToInt64(), out var peerId))
            {
                Debug.Log(_tag + "peer id=" + peerId + " auth status=" + info.ClientAuthStatus);
            }
        }

        private void HandleClientIntegrityViolated(ref OnClientIntegrityViolatedCallbackInfo info)
        {
            var reason = "local integrity violation " + info.ViolationType + ": " + info.ViolationMessage;
            Debug.LogError(_tag + reason);
            _fail("anti-cheat: " + reason);
        }

        private static string LaunchHint()
        {
            return "; launch the game through the EAC bootstrapper (start_protected_game) or pass "
                + AntiCheatSettings.NoEacFlag;
        }
    }
}

using System;
using System.Collections.Generic;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using Shooter.Core;
using Shooter.Protocol;
using UnityEngine;

namespace Shooter.NetEos
{
    // プラットフォームはプロセスに 1 つで終了時に EosPlatform が手放すので、CloseAll では手放さない。
    internal sealed class EosClientP2P : IEosP2P
    {
        private const string _displayName = "shooter-client";

        private readonly EosCredentials _credentials;
        private readonly Queue<EosP2PNotice> _notices = new Queue<EosP2PNotice>();
        private readonly Dictionary<string, ProductUserId> _remotes = new Dictionary<string, ProductUserId>(StringComparer.Ordinal);
        private P2PInterface _p2p;
        private EosDeviceLogin _login;
        private ProductUserId _localUserId;
        private bool _started;
        private bool _acceptsIncoming;
        private ulong _requestNotification;
        private ulong _establishedNotification;
        private ulong _closedNotification;

        public EosClientP2P(EosCredentials credentials)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        }

        private static SocketId Socket => new SocketId { SocketName = EosMatchTransport.SocketName };

        private bool CanCall => _started && EosPlatform.Current != null && _p2p != null && _localUserId != null;

        public bool TryBegin(bool acceptsIncoming, out string problem)
        {
            if (_started)
            {
                throw new InvalidOperationException("the EOS P2P is already started");
            }

            // SDK の関数 (ProductUserId.FromString を含む) はプラットフォームの初期化後に呼ぶ
            var platform = EosPlatform.Acquire(_credentials, out problem);
            if (platform == null)
            {
                return false;
            }

            var p2p = platform.GetP2PInterface();
            var connect = platform.GetConnectInterface();
            if (p2p == null || connect == null)
            {
                problem = "EOS P2P or Connect interface is unavailable";
                return false;
            }

            _started = true;
            _p2p = p2p;
            _acceptsIncoming = acceptsIncoming;

            var loggedIn = EosPlatform.LocalUserId;
            if (loggedIn != null)
            {
                // ロビーのために先にログインしていれば、同じユーザーでつなぐ
                Debug.Log("[Net] EOS reusing the device id login");
                HandleLoggedIn(loggedIn, null);
                return true;
            }

            Debug.Log("[Net] EOS logging in with the device id");
            _login = new EosDeviceLogin(connect, _displayName, SystemInfo.deviceModel);
            _login.Begin(HandleLoggedIn);
            return true;
        }

        public void Tick()
        {
            EosPlatform.Tick();
        }

        public bool TryTakeNotice(out EosP2PNotice notice)
        {
            if (_notices.Count > 0)
            {
                notice = _notices.Dequeue();
                return true;
            }

            notice = default;
            return false;
        }

        public bool TryReceivePacket(byte[] buffer, out string remoteUserId, out byte channel, out int length)
        {
            remoteUserId = null;
            channel = 0;
            length = 0;
            if (!CanCall)
            {
                return false;
            }

            var options = new ReceivePacketOptions
            {
                LocalUserId = _localUserId,
                MaxDataSizeBytes = (uint)Math.Min(buffer.Length, EosMatchTransport.MaxPacketSize),
                RequestedChannel = null,
            };
            ProductUserId peer = null;
            var socket = default(SocketId);
            var result = _p2p.ReceivePacket(ref options, ref peer, ref socket, out channel, new ArraySegment<byte>(buffer), out var written);
            if (result != Result.Success)
            {
                if (result != Result.NotFound)
                {
                    Debug.LogWarning("[Net] EOS ReceivePacket failed: " + result);
                }

                return false;
            }

            remoteUserId = Remember(peer);
            length = (int)written;
            return remoteUserId != null;
        }

        public bool SendPacket(string remoteUserId, byte channel, bool reliable, ArraySegment<byte> packet, out string problem)
        {
            var remote = Find(remoteUserId);
            if (!CanCall || remote == null)
            {
                problem = "not connected";
                return false;
            }

            var options = new SendPacketOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = remote,
                SocketId = Socket,
                Channel = channel,
                Data = packet,
                AllowDelayedDelivery = true,
                Reliability = reliable ? PacketReliability.ReliableOrdered : PacketReliability.UnreliableUnordered,
                // 接続は受け入れ・要求の手順でだけ開く。切断した相手への送信で勝手に開き直さない
                DisableAutoAcceptConnection = true,
            };
            var result = _p2p.SendPacket(ref options);
            problem = result == Result.Success ? null : result.ToString();
            return result == Result.Success;
        }

        public void AcceptConnection(string remoteUserId)
        {
            var remote = Find(remoteUserId);
            if (!CanCall || remote == null)
            {
                return;
            }

            var options = new AcceptConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = remote,
                SocketId = Socket,
            };
            var result = _p2p.AcceptConnection(ref options);
            if (result != Result.Success)
            {
                Debug.LogWarning("[Net] EOS AcceptConnection with " + remoteUserId + " failed: " + result);
            }
        }

        public void CloseConnection(string remoteUserId)
        {
            var remote = Find(remoteUserId);
            if (!CanCall || remote == null)
            {
                return;
            }

            var options = new CloseConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = remote,
                SocketId = Socket,
            };
            var result = _p2p.CloseConnection(ref options);
            if (result != Result.Success && result != Result.NotFound)
            {
                Debug.LogWarning("[Net] EOS CloseConnection with " + remoteUserId + " failed: " + result);
            }

            _remotes.Remove(remoteUserId);
        }

        public void CloseAll()
        {
            _login?.Cancel();
            _login = null;
            if (CanCall)
            {
                RemoveNotifications();
                var options = new CloseConnectionsOptions
                {
                    LocalUserId = _localUserId,
                    SocketId = Socket,
                };
                var result = _p2p.CloseConnections(ref options);
                if (result != Result.Success && result != Result.NotFound)
                {
                    Debug.LogWarning("[Net] EOS CloseConnections failed: " + result);
                }
            }

            _notices.Clear();
            _remotes.Clear();
            _localUserId = null;
            _p2p = null;
            _started = false;
        }

        private void HandleLoggedIn(ProductUserId userId, string error)
        {
            _login = null;
            if (!_started)
            {
                return;
            }

            if (error != null)
            {
                _notices.Enqueue(EosP2PNotice.SignInFailed(error));
                return;
            }

            _localUserId = userId;
            if (!Subscribe())
            {
                RemoveNotifications();
                _localUserId = null;
                _notices.Enqueue(EosP2PNotice.SignInFailed("EOS P2P notifications could not be registered"));
                return;
            }

            _notices.Enqueue(EosP2PNotice.SignedIn(userId.ToString()));
        }

        private bool Subscribe()
        {
            if (_acceptsIncoming)
            {
                var requestOptions = new AddNotifyPeerConnectionRequestOptions { LocalUserId = _localUserId, SocketId = Socket };
                _requestNotification = _p2p.AddNotifyPeerConnectionRequest(ref requestOptions, null, HandleConnectionRequest);
            }

            var establishedOptions = new AddNotifyPeerConnectionEstablishedOptions { LocalUserId = _localUserId, SocketId = Socket };
            _establishedNotification = _p2p.AddNotifyPeerConnectionEstablished(ref establishedOptions, null, HandleConnectionEstablished);

            var closedOptions = new AddNotifyPeerConnectionClosedOptions { LocalUserId = _localUserId, SocketId = Socket };
            _closedNotification = _p2p.AddNotifyPeerConnectionClosed(ref closedOptions, null, HandleConnectionClosed);

            return (!_acceptsIncoming || _requestNotification != 0) && _establishedNotification != 0 && _closedNotification != 0;
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
            var remote = Remember(info.RemoteUserId);
            if (remote != null)
            {
                _notices.Enqueue(EosP2PNotice.ConnectionRequested(remote));
            }
        }

        private void HandleConnectionEstablished(ref OnPeerConnectionEstablishedInfo info)
        {
            var remote = Remember(info.RemoteUserId);
            if (remote != null)
            {
                _notices.Enqueue(EosP2PNotice.ConnectionEstablished(remote));
            }
        }

        private void HandleConnectionClosed(ref OnRemoteConnectionClosedInfo info)
        {
            var remote = Remember(info.RemoteUserId);
            if (remote != null)
            {
                _notices.Enqueue(EosP2PNotice.ConnectionClosed(remote, info.Reason.ToString()));
                _remotes.Remove(remote);
            }
        }

        private string Remember(ProductUserId userId)
        {
            if (userId == null || !userId.IsValid())
            {
                return null;
            }

            var key = userId.ToString().ToLowerInvariant();
            if (!_remotes.ContainsKey(key))
            {
                _remotes[key] = userId;
            }

            return key;
        }

        private ProductUserId Find(string remoteUserId)
        {
            if (string.IsNullOrEmpty(remoteUserId))
            {
                return null;
            }

            if (_remotes.TryGetValue(remoteUserId, out var known))
            {
                return known;
            }

            if (EosPlatform.Current == null)
            {
                return null;
            }

            var parsed = ProductUserId.FromString(remoteUserId);
            if (parsed == null || !parsed.IsValid())
            {
                return null;
            }

            _remotes[remoteUserId] = parsed;
            return parsed;
        }
    }
}

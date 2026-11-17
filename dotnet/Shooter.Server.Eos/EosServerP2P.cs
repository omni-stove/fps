#if EOS_SDK
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using Shooter.Core;
using Shooter.Protocol;
// P2P と Connect の名前空間を両方開くと型名がぶつかりうるので、Connect は別名で参照する
using EosConnect = Epic.OnlineServices.Connect;

namespace Shooter.Server.Eos
{
    /// <summary>
    /// <see cref="EosMatchTransport"/> のために EOS の C# SDK の P2P を呼ぶ。プラットフォームとログインはロビーと
    /// <see cref="EosServerPlatform"/> で共有し、同じユーザーとして待ち受ける。
    /// </summary>
    /// <remarks>
    /// Connect の認証の期限が近づいたら Device ID でログインし直す。切れたままだと、つないでいる人は遊べても新しい人がつなげなくなるため。
    /// </remarks>
    internal sealed class EosServerP2P : IEosP2P
    {
        private const string _displayName = "shooter-server";

        private readonly EosCredentials _credentials;
        private readonly string _version;
        private readonly TextWriter _log;
        private readonly Queue<EosP2PNotice> _notices = new Queue<EosP2PNotice>();
        private readonly Dictionary<string, ProductUserId> _remotes = new Dictionary<string, ProductUserId>(StringComparer.Ordinal);
        private EosServerPlatform _platform;
        private P2PInterface _p2p;
        private EosConnect.ConnectInterface _connect;
        private EosDeviceLogin _login;
        private EosDeviceLogin _refresh;
        private ProductUserId _localUserId;
        private bool _acceptsIncoming;
        private ulong _requestNotification;
        private ulong _establishedNotification;
        private ulong _closedNotification;
        private ulong _authExpirationNotification;

        public EosServerP2P(EosCredentials credentials, string version, TextWriter log)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
        }

        private static string DeviceModel => "shooter-server " + RuntimeInformation.RuntimeIdentifier;

        private static SocketId Socket => new SocketId { SocketName = EosMatchTransport.SocketName };

        public bool TryBegin(bool acceptsIncoming, out string problem)
        {
            if (_platform != null)
            {
                throw new InvalidOperationException("the EOS P2P is already started");
            }

            var platform = EosServerPlatform.Acquire(_credentials, _version, _log, out problem);
            if (platform == null)
            {
                return false;
            }

            var p2p = platform.Interface.GetP2PInterface();
            var connect = platform.Interface.GetConnectInterface();
            if (p2p == null || connect == null)
            {
                platform.Release();
                problem = "EOS P2P or Connect interface is unavailable";
                return false;
            }

            _platform = platform;
            _p2p = p2p;
            _connect = connect;
            _acceptsIncoming = acceptsIncoming;

            var loggedIn = platform.LocalUserId;
            if (loggedIn != null && loggedIn.IsValid())
            {
                // ロビーのために先にログインしていれば、同じユーザーで待ち受ける
                _log.WriteLine("[Net] EOS reusing the device id login");
                HandleLoggedIn(loggedIn, null);
                return true;
            }

            _log.WriteLine("[Net] EOS logging in with the device id");
            _login = new EosDeviceLogin(connect, _displayName, DeviceModel);
            _login.Begin(HandleLoggedIn);
            return true;
        }

        public void Tick()
        {
            if (_platform != null && _platform.IsAlive)
            {
                _platform.Tick();
            }
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
                    _log.WriteLine("[Net] EOS ReceivePacket failed: " + result);
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
                _log.WriteLine("[Net] EOS AcceptConnection with " + remoteUserId + " failed: " + result);
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
                _log.WriteLine("[Net] EOS CloseConnection with " + remoteUserId + " failed: " + result);
            }
            _remotes.Remove(remoteUserId);
        }

        public void CloseAll()
        {
            _login?.Cancel();
            _login = null;
            _refresh?.Cancel();
            _refresh = null;
            if (_platform == null)
            {
                return;
            }

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
                    _log.WriteLine("[Net] EOS CloseConnections failed: " + result);
                }
            }

            _notices.Clear();
            _remotes.Clear();
            _localUserId = null;
            _p2p = null;
            _connect = null;
            _platform.Release();
            _platform = null;
        }

        private bool CanCall => _platform != null && _platform.IsAlive && _p2p != null && _localUserId != null;

        private void HandleLoggedIn(ProductUserId userId, string error)
        {
            _login = null;
            if (_platform == null || !_platform.IsAlive)
            {
                return;
            }

            if (error != null)
            {
                _notices.Enqueue(EosP2PNotice.SignInFailed(error));
                return;
            }

            _localUserId = userId;
            _platform.LocalUserId = userId;
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

            var authOptions = new EosConnect.AddNotifyAuthExpirationOptions();
            _authExpirationNotification = _connect.AddNotifyAuthExpiration(ref authOptions, null, HandleAuthExpiration);
            if (_authExpirationNotification == 0)
            {
                // つなぐことはできるので続ける。長く動かすと認証の期限が切れ、新しい人がつなげなくなる
                _log.WriteLine("[Net] EOS auth expiration notifications could not be registered; the login will not be refreshed");
            }

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

            if (_authExpirationNotification != 0)
            {
                _connect?.RemoveNotifyAuthExpiration(_authExpirationNotification);
                _authExpirationNotification = 0;
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

        private void HandleAuthExpiration(ref EosConnect.AuthExpirationCallbackInfo info)
        {
            if (_refresh != null || _connect == null || _localUserId == null || info.LocalUserId == null
                || !string.Equals(info.LocalUserId.ToString(), _localUserId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _refresh = new EosDeviceLogin(_connect, _displayName, DeviceModel);
            _refresh.Begin((userId, error) =>
            {
                _refresh = null;
                _log.WriteLine(error != null
                    ? "[Net] refreshing the EOS login failed: " + error
                    : "[Net] refreshed the EOS login user=" + userId);
            });
        }

        // 接続している間は SDK の Id を覚えて使い回し、閉じたら忘れて長く動かしても増え続けないようにする
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

            if (_platform == null || !_platform.IsAlive)
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
#endif

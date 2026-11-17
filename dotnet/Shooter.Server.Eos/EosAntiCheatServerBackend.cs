#if EOS_SDK
using System;
using System.IO;
using Epic.OnlineServices;
using Epic.OnlineServices.AntiCheatCommon;
using Epic.OnlineServices.AntiCheatServer;
using Shooter.Core;

namespace Shooter.Server.Eos
{
    // 専用サーバーはユーザーを持たないので、セッションはユーザー無しで始める。
    internal sealed class EosAntiCheatServerBackend : IAntiCheatServerBackend
    {
        // Unity のホストがピアの認証を待つ秒数 (EosAntiCheat.AuthenticationTimeoutSeconds) とそろえる。
        public const uint RegisterTimeoutSeconds = 60;

        private const string _serverName = "shooter-server";

        private readonly EosCredentials _credentials;
        private readonly string _version;
        private readonly TextWriter _log;
        private EosServerPlatform _platform;
        private AntiCheatServerInterface _server;
        private ulong _messageNotification;
        private ulong _actionNotification;
        private ulong _authNotification;
        private bool _sessionStarted;
        private bool _disposed;

        public EosAntiCheatServerBackend(EosCredentials credentials, string version, TextWriter log)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
        }

        public event Action<long, ArraySegment<byte>> MessageToClient;

        public event Action<long, bool, string> ClientActionRequired;

        public event Action<long, string> ClientAuthStatusChanged;

        public bool Begin(out string error)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_sessionStarted)
            {
                error = null;
                return true;
            }

            var platform = EosServerPlatform.Acquire(_credentials, _version, _log, out error);
            if (platform == null)
            {
                return false;
            }

            var server = platform.Interface.GetAntiCheatServerInterface();
            if (server == null)
            {
                platform.Release();
                error = "the EOS anti-cheat server interface is unavailable";
                return false;
            }

            _platform = platform;
            _server = server;
            if (!Subscribe())
            {
                Release();
                error = "anti-cheat notifications could not be registered";
                return false;
            }

            var options = new BeginSessionOptions
            {
                RegisterTimeoutSeconds = RegisterTimeoutSeconds,
                ServerName = _serverName,
                EnableGameplayData = false,
                LocalUserId = null,
            };
            var result = _server.BeginSession(ref options);
            if (result != Result.Success)
            {
                Release();
                error = "anti-cheat BeginSession failed: " + result
                    + " (check that Anti-Cheat and client-server protection are enabled for this deployment in the Epic Developer Portal)";
                return false;
            }

            _sessionStarted = true;
            error = null;
            return true;
        }

        public bool RegisterClient(long handle, string productUserId, out string error)
        {
            if (!CanCall(out error))
            {
                return false;
            }

            var userId = string.IsNullOrEmpty(productUserId) ? null : ProductUserId.FromString(productUserId);
            if (userId == null || !userId.IsValid())
            {
                error = "'" + productUserId + "' is not a valid EOS product user id";
                return false;
            }

            var options = new RegisterClientOptions
            {
                ClientHandle = new IntPtr(handle),
                ClientType = AntiCheatCommonClientType.ProtectedClient,
                ClientPlatform = AntiCheatCommonClientPlatform.Unknown,
                UserId = userId,
                IpAddress = null,
            };
            return Check(_server.RegisterClient(ref options), "RegisterClient", out error);
        }

        public bool UnregisterClient(long handle, out string error)
        {
            if (!CanCall(out error))
            {
                return false;
            }

            var options = new UnregisterClientOptions { ClientHandle = new IntPtr(handle) };
            return Check(_server.UnregisterClient(ref options), "UnregisterClient", out error);
        }

        public bool ReceiveMessageFromClient(long handle, ArraySegment<byte> message, out string error)
        {
            if (!CanCall(out error))
            {
                return false;
            }

            var options = new ReceiveMessageFromClientOptions { ClientHandle = new IntPtr(handle), Data = message };
            return Check(_server.ReceiveMessageFromClient(ref options), "ReceiveMessageFromClient", out error);
        }

        public void Tick()
        {
            if (!_disposed && _platform != null && _platform.IsAlive)
            {
                _platform.Tick();
            }
        }

        public bool End(out string error)
        {
            error = null;
            if (_sessionStarted && _server != null && _platform != null && _platform.IsAlive)
            {
                var options = new EndSessionOptions();
                Check(_server.EndSession(ref options), "EndSession", out error);
            }

            _sessionStarted = false;
            Release();
            return error == null;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            End(out _);
            _disposed = true;
        }

        private bool CanCall(out string error)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_sessionStarted || _server == null || _platform == null || !_platform.IsAlive)
            {
                error = "the anti-cheat session is not running";
                return false;
            }

            error = null;
            return true;
        }

        private static bool Check(Result result, string call, out string error)
        {
            error = result == Result.Success ? null : call + " failed: " + result;
            return error == null;
        }

        private bool Subscribe()
        {
            var messageOptions = new AddNotifyMessageToClientOptions();
            _messageNotification = _server.AddNotifyMessageToClient(ref messageOptions, null, HandleMessageToClient);

            var actionOptions = new AddNotifyClientActionRequiredOptions();
            _actionNotification = _server.AddNotifyClientActionRequired(ref actionOptions, null, HandleClientActionRequired);

            var authOptions = new AddNotifyClientAuthStatusChangedOptions();
            _authNotification = _server.AddNotifyClientAuthStatusChanged(ref authOptions, null, HandleClientAuthStatusChanged);

            return _messageNotification != 0 && _actionNotification != 0 && _authNotification != 0;
        }

        private void Release()
        {
            if (_server != null && _platform != null && _platform.IsAlive)
            {
                if (_messageNotification != 0)
                {
                    _server.RemoveNotifyMessageToClient(_messageNotification);
                }

                if (_actionNotification != 0)
                {
                    _server.RemoveNotifyClientActionRequired(_actionNotification);
                }

                if (_authNotification != 0)
                {
                    _server.RemoveNotifyClientAuthStatusChanged(_authNotification);
                }
            }

            _messageNotification = 0;
            _actionNotification = 0;
            _authNotification = 0;
            _server = null;
            _platform?.Release();
            _platform = null;
        }

        private void HandleMessageToClient(ref OnMessageToClientCallbackInfo info)
        {
            if (!_disposed)
            {
                MessageToClient?.Invoke(info.ClientHandle.ToInt64(), info.MessageData);
            }
        }

        private void HandleClientActionRequired(ref OnClientActionRequiredCallbackInfo info)
        {
            if (_disposed)
            {
                return;
            }

            var detailsText = info.ActionReasonDetailsString?.ToString();
            var details = string.IsNullOrEmpty(detailsText) ? "" : " (" + detailsText + ")";
            ClientActionRequired?.Invoke(
                info.ClientHandle.ToInt64(),
                info.ClientAction == AntiCheatCommonClientAction.RemovePlayer,
                info.ClientAction + " " + info.ActionReasonCode + details);
        }

        private void HandleClientAuthStatusChanged(ref OnClientAuthStatusChangedCallbackInfo info)
        {
            if (!_disposed)
            {
                ClientAuthStatusChanged?.Invoke(info.ClientHandle.ToInt64(), info.ClientAuthStatus.ToString());
            }
        }
    }
}
#endif

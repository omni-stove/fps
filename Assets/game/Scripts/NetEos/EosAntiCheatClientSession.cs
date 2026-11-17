using System;
using Epic.OnlineServices;
using Epic.OnlineServices.AntiCheatClient;
using Shooter.NetV2;
using UnityEngine;

namespace Shooter.NetEos
{
    // Anti-Cheat Client を ClientServer モードで始めるのは、.NET のサーバーが Anti-Cheat Server インターフェースでクライアントを登録するため。
    internal sealed class EosAntiCheatClientSession : IAntiCheatClientSession
    {
        private AntiCheatClientInterface _client;
        private Action<ArraySegment<byte>> _sendToServer;
        private Action<string> _fail;
        private ulong _messageNotification;
        private ulong _violationNotification;
        private bool _sessionStarted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Install()
        {
            NetV2AntiCheat.CreateSession = () => new EosAntiCheatClientSession();
        }

        public bool BeginSession(Action<ArraySegment<byte>> sendToServer, Action<string> fail, out string reason)
        {
            _sendToServer = sendToServer ?? throw new ArgumentNullException(nameof(sendToServer));
            _fail = fail ?? throw new ArgumentNullException(nameof(fail));
            reason = null;
            if (_sessionStarted)
            {
                return true;
            }

            var platform = EosPlatform.Current;
            var localUserId = EosPlatform.LocalUserId;
            if (platform == null || localUserId == null)
            {
                reason = "anti-cheat needs an EOS login, which only the EOS transport makes (-transport eos)";
                return false;
            }

            _client = platform.GetAntiCheatClientInterface();
            if (_client == null)
            {
                reason = "the EOS anti-cheat client interface is unavailable";
                return false;
            }

            var messageOptions = new AddNotifyMessageToServerOptions();
            _messageNotification = _client.AddNotifyMessageToServer(ref messageOptions, null, HandleMessageToServer);
            var violationOptions = new AddNotifyClientIntegrityViolatedOptions();
            _violationNotification = _client.AddNotifyClientIntegrityViolated(ref violationOptions, null, HandleClientIntegrityViolated);
            if (_messageNotification == 0 || _violationNotification == 0)
            {
                RemoveNotifications();
                _client = null;
                reason = "anti-cheat notifications could not be registered";
                return false;
            }

            var options = new BeginSessionOptions
            {
                LocalUserId = localUserId,
                Mode = AntiCheatClientMode.ClientServer,
            };
            var result = _client.BeginSession(ref options);
            if (result != Result.Success)
            {
                RemoveNotifications();
                _client = null;
                reason = "anti-cheat BeginSession failed: " + result;
                return false;
            }

            _sessionStarted = true;
            return true;
        }

        public bool ReceiveMessageFromServer(ArraySegment<byte> message, out string reason)
        {
            if (!_sessionStarted)
            {
                reason = "the anti-cheat session is not running";
                return false;
            }

            var options = new ReceiveMessageFromServerOptions { Data = message };
            var result = _client.ReceiveMessageFromServer(ref options);
            reason = result == Result.Success ? null : result.ToString();
            return reason == null;
        }

        public void EndSession()
        {
            if (_client == null)
            {
                return;
            }

            RemoveNotifications();
            if (_sessionStarted)
            {
                var options = new EndSessionOptions();
                var result = _client.EndSession(ref options);
                if (result != Result.Success)
                {
                    Debug.LogWarning(Shooter.Core.AntiCheatSettings.LogTag + " EndSession failed: " + result);
                }
            }

            _sessionStarted = false;
            _client = null;
        }

        private void RemoveNotifications()
        {
            if (_messageNotification != 0)
            {
                _client.RemoveNotifyMessageToServer(_messageNotification);
                _messageNotification = 0;
            }

            if (_violationNotification != 0)
            {
                _client.RemoveNotifyClientIntegrityViolated(_violationNotification);
                _violationNotification = 0;
            }
        }

        private void HandleMessageToServer(ref OnMessageToServerCallbackInfo info)
        {
            if (_sessionStarted)
            {
                _sendToServer(info.MessageData);
            }
        }

        private void HandleClientIntegrityViolated(ref OnClientIntegrityViolatedCallbackInfo info)
        {
            if (_sessionStarted)
            {
                _fail("local integrity violation " + info.ViolationType + ": " + info.ViolationMessage);
            }
        }
    }
}

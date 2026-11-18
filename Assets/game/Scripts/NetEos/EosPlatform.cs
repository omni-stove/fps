using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using Shooter.Core;
using UnityEngine;
// Platform と RTC・RTC Audio の名前空間を両方開くと型名がぶつかりうるので、RTC と RTC Audio は別名で参照する
using EosRtc = Epic.OnlineServices.RTC;
using EosRtcAudio = Epic.OnlineServices.RTCAudio;

namespace Shooter.NetEos
{
    // PlayEveryWare の EOSManager は StreamingAssets の設定ファイルから認証情報を読むので使わず、SDK を直接呼ぶ。
    // 認証情報をコミットせずに済むよう、呼び出し側から受け取る。
    internal static class EosPlatform
    {
        private const string _productName = "shooter";

        private static PlatformInterface _platform;
        private static ProductUserId _localUserId;
        private static bool _quitHooked;
        private static int _lastTickFrame = -1;
        private static bool _hasEncryptionKey;

        public static PlatformInterface Current => _platform;

        // Player Data Storage は鍵を渡して作ったプラットフォームでしか使えず、鍵は作った後から足せないので覚えておく。
        public static bool HasEncryptionKey => _platform != null && _hasEncryptionKey;

        public static EosRtc.RTCInterface Rtc => _platform?.GetRTCInterface();

        public static EosRtcAudio.RTCAudioInterface RtcAudio => Rtc?.GetAudioInterface();

        // ロビーと transport が同じユーザーを使い、ログインを 1 回で済ませるために持つ。
        public static ProductUserId LocalUserId => _platform != null && _localUserId != null && _localUserId.IsValid() ? _localUserId : null;

        public static void RememberLogin(ProductUserId userId)
        {
            if (_platform != null && userId != null && userId.IsValid())
            {
                _localUserId = userId;
            }
        }

        public static void ForgetLogin(ProductUserId userId)
        {
            if (_localUserId != null && userId != null
                && string.Equals(_localUserId.ToString(), userId.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                _localUserId = null;
            }
        }

        public static PlatformInterface Acquire(EosCredentials credentials, out string error)
        {
            if (credentials == null)
            {
                throw new ArgumentNullException(nameof(credentials));
            }

            if (_platform != null)
            {
                error = null;
                return _platform;
            }

            try
            {
                var initializeOptions = new InitializeOptions
                {
                    ProductName = _productName,
                    ProductVersion = string.IsNullOrEmpty(Application.version) ? "0" : Application.version,
                };
                var initialized = PlatformInterface.Initialize(ref initializeOptions);
                // エディタでは Play Mode をまたいで SDK が初期化済みのまま残る
                if (initialized != Result.Success && initialized != Result.AlreadyConfigured)
                {
                    error = "EOS SDK initialize failed: " + initialized;
                    return null;
                }

                LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Warning);
                LoggingInterface.SetCallback(HandleLog);

                var flags = PlatformFlags.DisableOverlay;
                if (Application.isEditor)
                {
                    flags |= PlatformFlags.LoadingInEditor;
                }

                var options = new Options
                {
                    ProductId = credentials.ProductId,
                    SandboxId = credentials.SandboxId,
                    DeploymentId = credentials.DeploymentId,
                    ClientCredentials = new ClientCredentials
                    {
                        ClientId = credentials.ClientId,
                        ClientSecret = credentials.ClientSecret,
                    },
                    IsServer = false,
                    Flags = flags,
                    CacheDirectory = Application.temporaryCachePath,
                    // RTCOptions を渡さないと GetRTCInterface が null を返し、ロビーの RTC 部屋 (ボイス) に入れない
                    RTCOptions = new RTCOptions { BackgroundMode = RTCBackgroundMode.LeaveRooms },
                    EncryptionKey = credentials.HasEncryptionKey ? credentials.EncryptionKey : null,
                };
                _platform = PlatformInterface.Create(ref options);
                _hasEncryptionKey = _platform != null && credentials.HasEncryptionKey;
            }
            catch (Exception e)
            {
                // ネイティブライブラリの読み込み失敗 (DllNotFoundException など) を含め、SDK との境界なので全部ここで受ける
                error = "EOS SDK could not be initialized: " + e.GetType().Name + ": " + e.Message;
                return null;
            }

            if (_platform == null)
            {
                error = "EOS platform could not be created; check the EOS_* product, sandbox, deployment and client ids";
                return null;
            }

            if (!_quitHooked)
            {
                _quitHooked = true;
                Application.quitting += Release;
            }

            error = null;
            return _platform;
        }

        // ロビーと transport の両方から呼ばれるので、1 フレームに 1 回だけ進める。
        public static void Tick()
        {
            if (_platform == null || _lastTickFrame == Time.frameCount)
            {
                return;
            }

            _lastTickFrame = Time.frameCount;
            _platform.Tick();
        }

        private static void Release()
        {
            if (_platform == null)
            {
                return;
            }

            _platform.Release();
            _platform = null;
            _localUserId = null;
            _lastTickFrame = -1;
            _hasEncryptionKey = false;

            // Shutdown 後は同じプロセスで初期化し直せないので、Play Mode を繰り返すエディタでは呼ばない
            if (!Application.isEditor)
            {
                PlatformInterface.Shutdown();
            }
        }

        private static void HandleLog(ref LogMessage message)
        {
            var text = "[EOS] " + message.Category + ": " + message.Message;
            if (message.Level <= LogLevel.Error)
            {
                Debug.LogError(text);
            }
            else
            {
                Debug.LogWarning(text);
            }
        }
    }
}

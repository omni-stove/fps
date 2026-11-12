using System;
using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using Fps.Core;
using UnityEngine;

namespace Fps.NetEos
{
    /// <summary>
    /// プロセスに 1 つの EOS プラットフォームを作って持つ。認証情報はコミットせず、呼び出し側から受け取る。
    /// PlayEveryWare の EOSManager (StreamingAssets の設定ファイルを読む) は使わず、SDK を直接呼ぶ。
    /// </summary>
    internal static class EosPlatform
    {
        private const string _productName = "fps";

        private static PlatformInterface _platform;
        private static ProductUserId _localUserId;
        private static bool _quitHooked;
        private static int _lastTickFrame = -1;

        /// <summary>作成済みのプラットフォーム。無ければ null。</summary>
        public static PlatformInterface Current => _platform;

        /// <summary>
        /// このプロセスで Device ID のログインに成功したユーザー。まだなら null。
        /// ロビーと transport が同じユーザーを使い、ログインを 1 回で済ませるために持つ。
        /// </summary>
        public static ProductUserId LocalUserId => _platform != null && _localUserId != null && _localUserId.IsValid() ? _localUserId : null;

        /// <summary><see cref="EosDeviceLogin"/> がログインに成功したときに呼ぶ。</summary>
        public static void RememberLogin(ProductUserId userId)
        {
            if (_platform != null && userId != null && userId.IsValid())
            {
                _localUserId = userId;
            }
        }

        /// <summary>
        /// プラットフォームを作る (作成済みならそれを返す)。SDK の初期化・ネイティブライブラリの読み込みの失敗は
        /// 例外にせず <paramref name="error"/> で返す。
        /// </summary>
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
                };
                _platform = PlatformInterface.Create(ref options);
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

        /// <summary>
        /// SDK のコールバックを進める。毎フレーム呼ぶ。ロビーと transport の両方から呼ばれるので、1 フレームに 1 回だけ進める。
        /// </summary>
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

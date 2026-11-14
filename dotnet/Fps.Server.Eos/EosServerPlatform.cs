#if EOS_SDK
using System;
using System.IO;
using Epic.OnlineServices;
using Epic.OnlineServices.Logging;
using Epic.OnlineServices.Platform;
using Fps.Core;

namespace Fps.Server.Eos
{
    /// <summary>
    /// SDK の初期化と終了はプロセスに 1 回しかできないので、プラットフォームは 1 つを数えて共有し、
    /// 最後の利用者が手放したところで解放して SDK を終える。ロビーと (後から足す) P2P の transport が同じ 1 つを使う。
    /// </summary>
    internal sealed class EosServerPlatform
    {
        private const string _productName = "fps-server";

        private static readonly object _gate = new object();
        private static EosServerPlatform _shared;
        private static TextWriter _sdkLog;

        private int _users;

        private EosServerPlatform(PlatformInterface platform)
        {
            Interface = platform;
        }

        public PlatformInterface Interface { get; private set; }

        public bool IsAlive => Interface != null;

        /// <summary>このプロセスで Device ID のログインに成功したユーザー。ロビーと transport でログインを 1 回で済ませるために持つ。</summary>
        public ProductUserId LocalUserId { get; set; }

        /// <summary>
        /// SDK の初期化・ネイティブライブラリの読み込みの失敗は例外にせず <paramref name="error"/> で返す。
        /// 返したものは <see cref="Release"/> で手放す。
        /// </summary>
        public static EosServerPlatform Acquire(EosCredentials credentials, string version, TextWriter log, out string error)
        {
            ArgumentNullException.ThrowIfNull(credentials);
            ArgumentNullException.ThrowIfNull(log);

            lock (_gate)
            {
                if (_shared != null)
                {
                    _shared._users++;
                    error = null;
                    return _shared;
                }

                PlatformInterface platform;
                try
                {
                    var initializeOptions = new InitializeOptions
                    {
                        ProductName = _productName,
                        ProductVersion = string.IsNullOrEmpty(version) ? "0" : version,
                    };
                    var initialized = PlatformInterface.Initialize(ref initializeOptions);
                    if (initialized != Result.Success && initialized != Result.AlreadyConfigured)
                    {
                        error = "EOS SDK initialize failed: " + initialized;
                        return null;
                    }

                    _sdkLog = log;
                    LoggingInterface.SetLogLevel(LogCategory.AllCategories, LogLevel.Warning);
                    LoggingInterface.SetCallback(HandleLog);

                    var cacheDirectory = Path.Combine(Path.GetTempPath(), "fps-server-eos");
                    Directory.CreateDirectory(cacheDirectory);
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
                        // ロビーは Connect でログインしたユーザーとして作るので、ユーザーを持たないサーバーのプラットフォームにはしない
                        IsServer = false,
                        Flags = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay,
                        CacheDirectory = cacheDirectory,
                    };
                    platform = PlatformInterface.Create(ref options);
                }
#pragma warning disable CA1031 // ネイティブライブラリの読み込み失敗 (DllNotFoundException など) を含め、SDK との境界なので全部ここで受ける
                catch (Exception e)
                {
                    error = "EOS SDK could not be initialized: " + e.GetType().Name + ": " + e.Message;
                    return null;
                }
#pragma warning restore CA1031

                if (platform == null)
                {
                    error = "EOS platform could not be created; check the EOS_* product, sandbox, deployment and client ids";
                    return null;
                }

                _shared = new EosServerPlatform(platform) { _users = 1 };
                error = null;
                return _shared;
            }
        }

        public void Tick()
        {
            Interface?.Tick();
        }

        public void Release()
        {
            lock (_gate)
            {
                if (Interface == null || --_users > 0)
                {
                    return;
                }

                Interface.Release();
                Interface = null;
                LocalUserId = null;
                _shared = null;
                PlatformInterface.Shutdown();
            }
        }

        private static void HandleLog(ref LogMessage message)
        {
            _sdkLog?.WriteLine("[EOS] " + message.Category + ": " + message.Message);
        }
    }
}
#endif

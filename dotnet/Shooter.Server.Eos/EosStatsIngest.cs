#if EOS_SDK
using System;
using System.Collections.Generic;
using System.IO;
using Epic.OnlineServices;
using Epic.OnlineServices.Stats;
using Shooter.Core;

namespace Shooter.Server.Eos
{
    // 送る相手 (TargetUserId) はプレイヤーで、送り手 (LocalUserId) は null にする。EOS では null が「信頼された専用サーバーからの送信」を表し、
    // ポータルのクライアントのポリシーで Stats の ingest を他人に対して許したときだけ通る。
    internal sealed class EosStatsIngest : IStatsIngestBackend
    {
        private readonly EosCredentials _credentials;
        private readonly string _version;
        private readonly TextWriter _log;
        private EosServerPlatform _platform;
        private StatsInterface _stats;
        private bool _disposed;

        public EosStatsIngest(EosCredentials credentials, string version, TextWriter log)
        {
            _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
        }

        private bool PlatformAlive => _platform != null && _platform.IsAlive;

        public void Tick()
        {
            if (!_disposed && PlatformAlive)
            {
                _platform.Tick();
            }
        }

        public void Ingest(string productUserId, IReadOnlyList<StatIngest> stats, Action<string, bool> done)
        {
            ArgumentNullException.ThrowIfNull(done);
            ArgumentNullException.ThrowIfNull(stats);
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (!EnsurePlatform(out var platformError))
            {
                // 初期化はネットワークに依らず失敗するので、同じ試合の分をやり直しても通らない。次の試合の送信でまた作り直す
                done(platformError, false);
                return;
            }

            var target = ProductUserId.FromString(productUserId);
            if (target == null || !target.IsValid())
            {
                done("'" + productUserId + "' is not a valid EOS product user id", false);
                return;
            }

            var data = new IngestData[stats.Count];
            for (var i = 0; i < data.Length; i++)
            {
                data[i] = new IngestData { StatName = stats[i].StatName, IngestAmount = stats[i].Amount };
            }

            var options = new IngestStatOptions
            {
                LocalUserId = null,
                TargetUserId = target,
                Stats = data,
            };
            _stats.IngestStat(ref options, null, (ref IngestStatCompleteCallbackInfo info) =>
            {
                if (_disposed)
                {
                    return;
                }

                var result = info.ResultCode;
                if (result == Result.Success)
                {
                    done(null, false);
                    return;
                }

                done("ingesting EOS stats failed: " + result, IsTransient(result));
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stats = null;
            _platform?.Release();
            _platform = null;
        }

        private bool EnsurePlatform(out string error)
        {
            if (PlatformAlive && _stats != null)
            {
                error = null;
                return true;
            }

            // 解放された共有のプラットフォームを持ち続けないよう、作り直す前に手放す
            _platform?.Release();
            _platform = null;
            _stats = null;

            var platform = EosServerPlatform.Acquire(_credentials, _version, _log, out error);
            if (platform == null)
            {
                return false;
            }

            var stats = platform.Interface.GetStatsInterface();
            if (stats == null)
            {
                platform.Release();
                error = "EOS Stats interface is unavailable";
                return false;
            }

            _platform = platform;
            _stats = stats;
            return true;
        }

        private static bool IsTransient(Result result)
        {
            return result == Result.TooManyRequests
                || result == Result.TimedOut
                || result == Result.NoConnection
                || result == Result.ServiceFailure;
        }
    }
}
#endif

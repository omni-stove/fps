using System;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // UI (Shooter.Runtime.UI) は NetV2 を参照しないので、開く口を GameSignals.LeaderboardControl に、結果を GameSignals.Leaderboard に出す。
    // 回す側 (メニューに残る部品) が Tick を呼ぶ。
    public sealed class NetV2Leaderboard : ILeaderboardControl, IDisposable
    {
        private readonly ClientLeaderboard _leaderboard;
        private bool _disposed;

        public NetV2Leaderboard(ILeaderboardBackend backend, LeaderboardSpec spec, Action<string> log)
        {
            _leaderboard = new ClientLeaderboard(backend, spec, GameSignals.Leaderboard.Publish, log);
            GameSignals.LeaderboardControl.Publish(this);
        }

        public ClientLeaderboard Leaderboard => _leaderboard;

        // EOS が無いビルドや認証情報が無いときは作らず、口を出さないことで画面に「使えない」と出させる。
        public static NetV2Leaderboard Create(LeaderboardSpec spec, Action<string> log)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            if (log == null)
            {
                throw new ArgumentNullException(nameof(log));
            }

            var factory = NetV2Eos.Factory;
            if (factory == null)
            {
                log(LeaderboardLog.Off("EOS is not in this build"));
                return null;
            }

            if (!factory.TryLoadCredentials(out var credentials, out var missing, out var loadError))
            {
                log(LeaderboardLog.Off("cannot load EOS credentials: " + loadError));
                return null;
            }

            if (credentials == null)
            {
                log(LeaderboardLog.Off("no EOS credentials; missing " + string.Join(", ", missing)));
                return null;
            }

            var backend = factory.CreateLeaderboardBackend(credentials);
            if (backend == null)
            {
                log(LeaderboardLog.Off("the EOS leaderboard factory returned no backend"));
                return null;
            }

            return new NetV2Leaderboard(backend, spec, log);
        }

        public bool Open(string boardId)
        {
            return !_disposed && _leaderboard.Open(boardId);
        }

        public bool Refresh()
        {
            return !_disposed && _leaderboard.Refresh();
        }

        public void Close()
        {
            _leaderboard.Close();
        }

        public void Tick(float deltaSeconds)
        {
            _leaderboard.Tick(deltaSeconds);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _leaderboard.Dispose();
            GameSignals.LeaderboardControl.ClearIfCurrent(this);
        }
    }
}

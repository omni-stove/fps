using System;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // UI (Shooter.Runtime.UI) は NetV2 を参照しないので、操る口を GameSignals.ServerBrowserControl に、一覧を GameSignals.ServerBrowser に出す。
    // 回す側 (メニューに残る NetV2OnlineServices) が Tick を呼ぶ。
    public sealed class NetV2ServerBrowser : IServerBrowserControl, IDisposable
    {
        private readonly ServerBrowser _browser;
        private readonly Func<int> _partySize;
        private readonly Func<ServerJoinResult, string> _connect;
        private readonly Action<string> _log;
        private bool _disposed;

        public NetV2ServerBrowser(
            Func<ILobbyBackend> createBackend,
            IServerPinger pinger,
            string version,
            Func<int> partySize,
            Func<ServerJoinResult, string> connect,
            Action<string> log)
        {
            _partySize = partySize ?? throw new ArgumentNullException(nameof(partySize));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _browser = new ServerBrowser(createBackend, pinger, version, GameSignals.ServerBrowser.Publish, log) { JoinsMatchLobby = true };
            GameSignals.ServerBrowserControl.Publish(this);
        }

        public ServerBrowser Browser => _browser;

        public bool Refresh()
        {
            return !_disposed && _browser.Refresh();
        }

        public void Join(string serverId, Action<ServerJoinOutcome> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetV2ServerBrowser));
            }

            _browser.Join(serverId, Math.Max(1, _partySize()), result => Finish(result, done));
        }

        public void Close()
        {
            if (!_disposed)
            {
                _browser.Close();
            }
        }

        public void Tick(float now)
        {
            _browser.Tick(now);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.ServerBrowserControl.ClearIfCurrent(this);
            _browser.Dispose();
        }

        // 試合ロビーは結果と一緒にこちらへ渡っているので、つながないときはここで閉じてロビーから出る
        private void Finish(ServerJoinResult result, Action<ServerJoinOutcome> done)
        {
            if (_disposed)
            {
                result.MatchLobby?.Dispose();
                return;
            }

            if (result.Outcome != ServerJoinOutcome.Joined)
            {
                done(result.Outcome);
                return;
            }

            _log(ServerBrowserLog.Connecting(result.Options.Endpoint));
            var refusal = _connect(result);
            if (refusal != null)
            {
                result.MatchLobby?.Dispose();
                _log(ServerBrowserLog.NotJoined(result.ServerId, ServerJoinOutcome.Failed, ServerBrowserLog.NotConnected(refusal)));
                done(ServerJoinOutcome.Failed);
                return;
            }

            done(ServerJoinOutcome.Joined);
        }
    }
}

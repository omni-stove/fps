using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Runtime;

namespace Shooter.NetV2
{
    // JoinCodeSearch は 1 回探すと終わり、渡したロビーのサービスを Dispose で閉じるので、入れるたびにサービスから作り直す。
    public sealed class NetV2JoinByCode : IJoinByCode, IDisposable
    {
        private readonly Func<ILobbyBackend> _createBackend;
        private readonly string _version;
        private readonly Func<int> _partySize;
        private readonly Func<JoinCodeResult, string> _connect;
        private readonly Action<string> _log;
        private readonly List<JoinCodeSearch> _retired = new List<JoinCodeSearch>();
        private JoinCodeSearch _search;
        private float _now;
        private bool _disposed;

        public NetV2JoinByCode(
            Func<ILobbyBackend> createBackend, string version, Func<int> partySize, Func<JoinCodeResult, string> connect, Action<string> log)
        {
            _createBackend = createBackend ?? throw new ArgumentNullException(nameof(createBackend));
            _partySize = partySize ?? throw new ArgumentNullException(nameof(partySize));
            _connect = connect ?? throw new ArgumentNullException(nameof(connect));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _version = version;
            GameSignals.JoinByCode.Publish(this);
        }

        public bool IsSearching => _search != null && !_search.IsFinished;

        public void Join(string code, Action<JoinCodeOutcome> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetV2JoinByCode));
            }

            // 2 つ目を始めると先の結果で別のサーバーにつなぎうるので、先のが終わるまで断る。
            if (IsSearching)
            {
                Refuse(JoinCodeLog.Busy(), done);
                return;
            }

            ILobbyBackend backend;
            try
            {
                backend = _createBackend();
            }
            catch (Exception e) when (e is InvalidOperationException || e is ArgumentException)
            {
                Refuse(JoinCodeLog.Unavailable(e.Message), done);
                return;
            }

            if (backend == null)
            {
                Refuse(JoinCodeLog.Unavailable("the EOS lobby factory returned no backend"), done);
                return;
            }

            Retire();
            var search = new JoinCodeSearch(backend, _version, _log) { JoinsMatchLobby = true };
            _search = search;
            search.Begin(_now, code, Math.Max(1, _partySize()), result => Finish(search, result, done));
        }

        public void Tick(float now)
        {
            if (_disposed)
            {
                return;
            }

            _now = now;
            foreach (var retired in _retired)
            {
                retired.Dispose();
            }

            _retired.Clear();
            if (_search == null)
            {
                return;
            }

            if (_search.IsFinished)
            {
                Retire();
                return;
            }

            _search.Tick(now);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            GameSignals.JoinByCode.ClearIfCurrent(this);
            Retire();
            foreach (var retired in _retired)
            {
                retired.Dispose();
            }

            _retired.Clear();
        }

        // 終わった検索は、結果を返した呼び戻しの中 (UI が続けて入れ直すときを含む) でロビーのサービスを閉じないよう、次の Tick まで閉じずに置く。
        private void Retire()
        {
            if (_search != null)
            {
                _retired.Add(_search);
                _search = null;
            }
        }

        private void Finish(JoinCodeSearch search, JoinCodeResult result, Action<JoinCodeOutcome> done)
        {
            // 試合ロビーは結果と一緒にこちらへ渡っているので、つながないときはここで閉じてロビーから出る
            if (_disposed || search != _search)
            {
                result.MatchLobby?.Dispose();
                return;
            }

            if (result.Outcome != JoinCodeOutcome.Joined)
            {
                done(result.Outcome);
                return;
            }

            _log(JoinCodeLog.Connecting(result.Options.Endpoint));
            var refusal = _connect(result);
            if (refusal != null)
            {
                result.MatchLobby?.Dispose();
                _log(JoinCodeLog.Finished(JoinCodeOutcome.Failed, JoinCodeLog.NotConnected(refusal)));
                done(JoinCodeOutcome.Failed);
                return;
            }

            done(JoinCodeOutcome.Joined);
        }

        private void Refuse(string reason, Action<JoinCodeOutcome> done)
        {
            _log(JoinCodeLog.Finished(JoinCodeOutcome.Failed, reason));
            done(JoinCodeOutcome.Failed);
        }
    }
}

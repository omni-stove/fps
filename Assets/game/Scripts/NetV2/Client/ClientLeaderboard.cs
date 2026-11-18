using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.NetV2
{
    // 問い合わせを 1 つずつにするのは、EOS が最後に問い合わせたボードの結果だけを手元に持ち、重ねると別のボードの行を読んでしまうため。
    public sealed class ClientLeaderboard : IDisposable
    {
        private readonly ILeaderboardBackend _backend;
        private readonly LeaderboardSpec _spec;
        private readonly Action<LeaderboardStatus?> _publish;
        private readonly Action<string> _log;
        private string _boardId;
        private string _localUserId;
        private LeaderboardStatus? _status;
        private bool _loggingIn;
        private bool _querying;
        private bool _queryAgain;
        private float _sinceLoaded;
        private bool _disposed;

        public ClientLeaderboard(ILeaderboardBackend backend, LeaderboardSpec spec, Action<LeaderboardStatus?> publish, Action<string> log)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
            _publish = publish ?? throw new ArgumentNullException(nameof(publish));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public LeaderboardSpec Spec => _spec;

        public string BoardId => _boardId;

        public string LocalUserId => _localUserId;

        public LeaderboardStatus? Status => _status;

        public bool IsBusy => _loggingIn || _querying;

        public bool Open(string boardId)
        {
            ThrowIfDisposed();
            var board = _spec.Find(boardId);
            if (board == null)
            {
                _log(LeaderboardLog.UnknownBoard(boardId));
                return false;
            }

            _boardId = board.Id;
            Publish(new LeaderboardStatus(board.Id, LeaderboardStage.Loading, null));
            Query();
            return true;
        }

        // 読み直す間も前の行を出したままにして、一覧が一瞬空になって見えないようにする
        public bool Refresh()
        {
            ThrowIfDisposed();
            if (_boardId == null || IsBusy)
            {
                return false;
            }

            var rows = _status.HasValue && _status.Value.BoardId == _boardId ? _status.Value.Rows : null;
            Publish(new LeaderboardStatus(_boardId, LeaderboardStage.Loading, rows));
            Query();
            return true;
        }

        public void Close()
        {
            if (_disposed)
            {
                return;
            }

            _boardId = null;
            _queryAgain = false;
            Publish(null);
        }

        public void Tick(float deltaSeconds)
        {
            if (_disposed)
            {
                return;
            }

            _backend.Tick();
            if (_boardId == null || IsBusy)
            {
                return;
            }

            _sinceLoaded += deltaSeconds;
            if (_sinceLoaded >= _spec.RefreshSeconds)
            {
                Refresh();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            Close();
            _disposed = true;
            _backend.Dispose();
        }

        // 上位に自分がいればその行に印を付け、いなければ順位の付いた自分の行を最後に足す。
        public static IReadOnlyList<LeaderboardRow> BuildRows(LeaderboardPage page, string localUserId, int maxRows)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            var rows = new List<LeaderboardRow>(Math.Min(page.Top.Count, maxRows) + 1);
            var localShown = false;
            foreach (var entry in page.Top)
            {
                if (rows.Count >= maxRows)
                {
                    break;
                }

                // 順位の無い行は EOS が集計を終えていない人なので並べない
                if (entry.Rank < 1)
                {
                    continue;
                }

                var isLocal = IsSameUser(entry.UserId, localUserId);
                localShown |= isLocal;
                rows.Add(new LeaderboardRow(entry.Rank, entry.DisplayName, entry.Score, isLocal));
            }

            var local = page.Local;
            if (!localShown && local.HasValue && local.Value.Rank >= 1 && IsSameUser(local.Value.UserId, localUserId))
            {
                rows.Add(new LeaderboardRow(local.Value.Rank, local.Value.DisplayName, local.Value.Score, true));
            }

            return rows;
        }

        private void Query()
        {
            if (_querying || _loggingIn)
            {
                _queryAgain = true;
                return;
            }

            if (_localUserId == null)
            {
                Login();
                return;
            }

            var boardId = _boardId;
            _querying = true;
            _queryAgain = false;
            _log(LeaderboardLog.Querying(boardId));
            _backend.QueryRanks(boardId, _spec.PageSize, _localUserId, (page, error) => FinishQuery(boardId, page, error));
        }

        private void Login()
        {
            _loggingIn = true;
            _log(LeaderboardLog.LoggingIn());
            _backend.Login((userId, error) =>
            {
                if (_disposed)
                {
                    return;
                }

                _loggingIn = false;
                if (error != null || string.IsNullOrEmpty(userId))
                {
                    var reason = error ?? "no user id was returned";
                    _log(LeaderboardLog.LoginFailed(reason));
                    _queryAgain = false;
                    _sinceLoaded = 0f;
                    if (_boardId != null)
                    {
                        Publish(new LeaderboardStatus(_boardId, LeaderboardStage.Unavailable, null));
                    }

                    return;
                }

                _localUserId = userId;
                _log(LeaderboardLog.LoggedIn(userId));
                _queryAgain = false;
                if (_boardId != null)
                {
                    Query();
                }
            });
        }

        private void FinishQuery(string boardId, LeaderboardPage page, string error)
        {
            if (_disposed)
            {
                return;
            }

            _querying = false;
            if (_boardId == null)
            {
                _queryAgain = false;
                return;
            }

            // 待っている間に別のボードを開いたか読み直しを頼まれたら、この結果は出さずに問い合わせ直す
            if (_queryAgain || !string.Equals(boardId, _boardId, StringComparison.Ordinal))
            {
                Query();
                return;
            }

            _sinceLoaded = 0f;
            if (error != null || page == null)
            {
                _log(LeaderboardLog.Failed(boardId, error ?? "no result was returned"));
                Publish(new LeaderboardStatus(boardId, LeaderboardStage.Unavailable, null));
                return;
            }

            var rows = BuildRows(page, _localUserId, _spec.PageSize);
            _log(LeaderboardLog.Loaded(boardId, rows.Count, LocalRankOf(rows)));
            Publish(new LeaderboardStatus(boardId, LeaderboardStage.Ready, rows));
        }

        private void Publish(LeaderboardStatus? status)
        {
            if (Nullable.Equals(_status, status))
            {
                return;
            }

            _status = status;
            _publish(status);
        }

        private static int? LocalRankOf(IReadOnlyList<LeaderboardRow> rows)
        {
            foreach (var row in rows)
            {
                if (row.IsLocal)
                {
                    return row.Rank;
                }
            }

            return null;
        }

        // EOS の ProductUserId は 16 進なので大文字と小文字を区別しない
        private static bool IsSameUser(string userId, string localUserId)
        {
            return !string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(localUserId)
                && string.Equals(userId, localUserId, StringComparison.OrdinalIgnoreCase);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ClientLeaderboard));
            }
        }
    }
}

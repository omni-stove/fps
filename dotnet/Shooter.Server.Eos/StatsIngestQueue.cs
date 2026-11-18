using System;
using System.Collections.Generic;
using System.IO;

namespace Shooter.Server.Eos
{
    // 1 件ずつ送るのは、試合の終わりに全員分をまとめて投げて EOS の呼び出しの上限 (TooManyRequests) に当たらないようにするため。
    internal sealed class StatsIngestQueue : IDisposable
    {
        public const int MaxAttempts = 3;

        public const float RetryDelaySeconds = 2f;

        public const float FlushWaitSeconds = 5f;

        public const int FlushPollMilliseconds = 10;

        private readonly IStatsIngestBackend _backend;
        private readonly TextWriter _log;
        private readonly Func<float> _clock;
        private readonly Action<int> _sleep;
        private readonly List<Entry> _pending = new List<Entry>();
        private Entry _sending;
        private int _sendToken;
        private bool _disposed;

        public StatsIngestQueue(IStatsIngestBackend backend, TextWriter log, Func<float> clock, Action<int> sleep)
        {
            _backend = backend ?? throw new ArgumentNullException(nameof(backend));
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _sleep = sleep ?? throw new ArgumentNullException(nameof(sleep));
        }

        // 送っている途中の 1 件も数える
        public int Pending => _pending.Count + (_sending != null ? 1 : 0);

        public int Sent { get; private set; }

        public int Dropped { get; private set; }

        public void Enqueue(string matchId, string productUserId, IReadOnlyList<StatIngest> stats)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (string.IsNullOrEmpty(productUserId))
            {
                throw new ArgumentException("productUserId must not be empty", nameof(productUserId));
            }

            ArgumentNullException.ThrowIfNull(stats);
            if (stats.Count == 0)
            {
                return;
            }

            // 呼び出し側が一覧を使い回しても、送るものが変わらないよう写す
            var copy = new StatIngest[stats.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = stats[i];
            }

            _pending.Add(new Entry(matchId ?? string.Empty, productUserId, copy));
        }

        public void Tick()
        {
            if (_disposed)
            {
                return;
            }

            _backend.Tick();
            TrySendNext();
        }

        public void FlushAndWait()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (Pending == 0)
            {
                return;
            }

            _log.WriteLine(StatsIngestLog.Flushing(Pending, FlushWaitSeconds));
            var deadline = _clock() + FlushWaitSeconds;
            while (Pending > 0 && _clock() < deadline)
            {
                Tick();
                if (Pending > 0)
                {
                    _sleep(FlushPollMilliseconds);
                }
            }

            if (Pending > 0)
            {
                _log.WriteLine(StatsIngestLog.FlushTimedOut(Pending));
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pending.Clear();
            _sending = null;
            _backend.Dispose();
        }

        private void TrySendNext()
        {
            if (_sending != null || _pending.Count == 0)
            {
                return;
            }

            var now = _clock();
            var index = _pending.FindIndex(e => e.NextAttemptAt <= now);
            if (index < 0)
            {
                return;
            }

            var entry = _pending[index];
            _pending.RemoveAt(index);
            _sending = entry;
            entry.Attempts++;
            var token = ++_sendToken;
            _backend.Ingest(entry.UserId, entry.Stats, (error, canRetry) => Finish(token, entry, error, canRetry));
        }

        private void Finish(int token, Entry entry, string error, bool canRetry)
        {
            // 後始末の後や、同じ送信の 2 回目の知らせは捨てる
            if (_disposed || token != _sendToken || _sending != entry)
            {
                return;
            }

            _sending = null;
            if (error == null)
            {
                Sent++;
                _log.WriteLine(StatsIngestLog.Sent(entry.MatchId, entry.UserId, entry.Stats));
                return;
            }

            if (canRetry && entry.Attempts < MaxAttempts)
            {
                var delay = RetryDelaySeconds * (1 << (entry.Attempts - 1));
                entry.NextAttemptAt = _clock() + delay;
                _log.WriteLine(StatsIngestLog.Retrying(entry.MatchId, entry.UserId, entry.Attempts, delay, error));

                // 失敗した 1 件を先頭で待たせると後ろが詰まるので、後ろへ回して待つ間に他の人の分を送る
                _pending.Add(entry);
                return;
            }

            Dropped++;
            _log.WriteLine(StatsIngestLog.Dropped(entry.MatchId, entry.UserId, entry.Attempts, error));
        }

        private sealed class Entry
        {
            public Entry(string matchId, string userId, StatIngest[] stats)
            {
                MatchId = matchId;
                UserId = userId;
                Stats = stats;
            }

            public string MatchId { get; }

            public string UserId { get; }

            public StatIngest[] Stats { get; }

            public int Attempts { get; set; }

            public float NextAttemptAt { get; set; } = float.NegativeInfinity;
        }
    }
}

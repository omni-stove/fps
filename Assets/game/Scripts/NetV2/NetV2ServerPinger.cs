using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;
using UnityPing = UnityEngine.Ping;

namespace Shooter.NetV2
{
    // サーバーに手を入れずに測れるよう、UDP の試合の口ではなく ICMP の echo (UnityEngine.Ping) で往復を測る。
    // ICMP を落とすサーバーは測れず、期限で null を返す。
    public sealed class NetV2ServerPinger : IServerPinger
    {
        private readonly float _timeoutSeconds;
        private readonly Func<float> _clock;
        private readonly List<Pending> _pending = new List<Pending>();
        private int _epoch;
        private bool _disposed;

        public NetV2ServerPinger()
            : this(LobbyLaunchOptions.ServerPingTimeoutSeconds, () => Time.unscaledTime)
        {
        }

        public NetV2ServerPinger(float timeoutSeconds, Func<float> clock)
        {
            if (!(timeoutSeconds > 0f) || float.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "must be a positive finite number");
            }

            _timeoutSeconds = timeoutSeconds;
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public int PendingCount => _pending.Count;

        public void Ping(string address, Action<int?> done)
        {
            if (done == null)
            {
                throw new ArgumentNullException(nameof(done));
            }

            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(NetV2ServerPinger));
            }

            UnityPing ping = null;
            if (!string.IsNullOrWhiteSpace(address))
            {
                try
                {
                    ping = new UnityPing(address);
                }
                catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is NotSupportedException)
                {
                    Debug.LogWarning(ServerBrowserLog.CannotPing(address, e.Message));
                }
            }

            // 測れないときも呼んだその場では返さず Tick で返し、返す順と呼ぶスレッドを測れたときとそろえる
            _pending.Add(new Pending(ping, _clock() + _timeoutSeconds, done));
        }

        public void Tick()
        {
            if (_disposed || _pending.Count == 0)
            {
                return;
            }

            var now = _clock();
            var finished = new List<Pending>();
            for (var i = 0; i < _pending.Count; i++)
            {
                var pending = _pending[i];
                var answered = pending.Ping == null || pending.Ping.isDone;
                if (answered || now >= pending.Deadline)
                {
                    finished.Add(pending);
                }
            }

            foreach (var pending in finished)
            {
                _pending.Remove(pending);
            }

            // 呼び戻しの中で測り直し (CancelAll・Ping) が呼ばれても回している一覧を崩さないよう、外してから返す。
            // CancelAll の後は、もう外した分も返さない。
            var epoch = _epoch;
            foreach (var pending in finished)
            {
                int? milliseconds = null;
                if (pending.Ping != null && pending.Ping.isDone && pending.Ping.time >= 0)
                {
                    milliseconds = pending.Ping.time;
                }

                pending.Ping?.DestroyPing();
                if (epoch == _epoch && !_disposed)
                {
                    pending.Done(milliseconds);
                }
            }
        }

        public void CancelAll()
        {
            _epoch++;
            foreach (var pending in _pending)
            {
                pending.Ping?.DestroyPing();
            }

            _pending.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CancelAll();
            _disposed = true;
        }

        private sealed class Pending
        {
            public Pending(UnityPing ping, float deadline, Action<int?> done)
            {
                Ping = ping;
                Deadline = deadline;
                Done = done;
            }

            public UnityPing Ping { get; }

            public float Deadline { get; }

            public Action<int?> Done { get; }
        }
    }
}

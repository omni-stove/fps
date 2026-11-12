using System;

namespace Fps.Core
{
    /// <summary>
    /// 一定の間隔で試し直し、全体の期限を過ぎたら諦めるための時刻の管理。時刻は呼び出し側が渡す (秒)。
    /// </summary>
    public sealed class RetrySchedule
    {
        private readonly float _intervalSeconds;
        private readonly float _timeoutSeconds;
        private float _deadline;
        private float _next;
        private bool _started;

        /// <param name="intervalSeconds">試し直すまでの間隔 (0 より大きいこと)。</param>
        /// <param name="timeoutSeconds"><see cref="Start"/> から諦めるまでの秒数 (0 より大きいこと)。</param>
        public RetrySchedule(float intervalSeconds, float timeoutSeconds)
        {
            if (!(intervalSeconds > 0f) || float.IsInfinity(intervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), intervalSeconds, "must be a positive finite number");
            }

            if (!(timeoutSeconds > 0f) || float.IsInfinity(timeoutSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), timeoutSeconds, "must be a positive finite number");
            }

            _intervalSeconds = intervalSeconds;
            _timeoutSeconds = timeoutSeconds;
        }

        public float TimeoutSeconds => _timeoutSeconds;

        public float IntervalSeconds => _intervalSeconds;

        /// <summary>期限を <paramref name="now"/> から数え始める。最初の試行はすぐ行ってよい。</summary>
        public void Start(float now)
        {
            _started = true;
            _deadline = now + _timeoutSeconds;
            _next = now;
        }

        /// <summary>期限を過ぎたか。<see cref="Start"/> の前は false。</summary>
        public bool IsExpired(float now)
        {
            return _started && now >= _deadline;
        }

        /// <summary>次の試行をしてよいか。<see cref="Start"/> の前は false。</summary>
        public bool IsDue(float now)
        {
            return _started && now >= _next;
        }

        /// <summary>試行が失敗したので、<see cref="IntervalSeconds"/> 待ってから次を試す。</summary>
        public void Wait(float now)
        {
            _next = now + _intervalSeconds;
        }
    }
}

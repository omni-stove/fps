using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class WaveRespawn : IRespawnPolicy
    {
        private readonly Dictionary<int, IndividualRespawn.Waiter> _waiters = new Dictionary<int, IndividualRespawn.Waiter>();

        // 波の時刻は間隔を足し続けずに (番号 × 間隔) で出し、誤差をためない。
        private double _elapsedSeconds;
        private long _wavesPassed;

        public float RespawnSeconds { get; }

        public float WaveIntervalSeconds { get; }

        public WaveRespawn(float respawnSeconds, float waveIntervalSeconds)
        {
            if (!(respawnSeconds >= 0f) || float.IsInfinity(respawnSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(respawnSeconds), respawnSeconds, "respawnSeconds must not be negative and must be finite");
            }

            if (!(waveIntervalSeconds > 0f) || float.IsInfinity(waveIntervalSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(waveIntervalSeconds), waveIntervalSeconds, "waveIntervalSeconds must be positive and finite");
            }

            RespawnSeconds = respawnSeconds;
            WaveIntervalSeconds = waveIntervalSeconds;
        }

        public float? SecondsUntilNextWave => (float)Math.Max(0d, WaveTime(_wavesPassed + 1) - _elapsedSeconds);

        public bool WillReturn(ModeSession session, int playerId) => true;

        public bool BeginWait(ModeSession session, int playerId)
        {
            _waiters[playerId] = new IndividualRespawn.Waiter(RespawnSeconds);
            return false;
        }

        public void Cancel(int playerId) => _waiters.Remove(playerId);

        public void CancelAll() => _waiters.Clear();

        public void Reset()
        {
            _waiters.Clear();
            _elapsedSeconds = 0d;
            _wavesPassed = 0;
        }

        public float? GetRemaining(int playerId)
        {
            if (!_waiters.TryGetValue(playerId, out var waiter))
            {
                return null;
            }

            var wait = Math.Max(0f, waiter.Remaining);
            var waveAt = WaveTime(FirstWaveAtOrAfter(_elapsedSeconds + wait));
            return (float)Math.Max(0d, waveAt - _elapsedSeconds);
        }

        public void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            if (respawn == null)
            {
                throw new ArgumentNullException(nameof(respawn));
            }

            _elapsedSeconds += elapsedSeconds;

            // 波を出す前に全員の待ちを減らしておき、波のハンドラの中で倒された人は今の時刻から数え始めるようにする。
            foreach (var waiter in _waiters.Values)
            {
                waiter.Remaining -= elapsedSeconds;
            }

            // 1 回の Tick で波を複数またいだときは、古い波から順に出す。
            while (session.State == GameModeState.Playing)
            {
                var waveAt = WaveTime(_wavesPassed + 1);
                if (waveAt > _elapsedSeconds)
                {
                    return;
                }

                _wavesPassed++;
                var sinceWave = (float)(_elapsedSeconds - waveAt);

                List<KeyValuePair<int, IndividualRespawn.Waiter>> ready = null;
                foreach (var pair in _waiters)
                {
                    if (IsReadyAtWave(pair.Value, sinceWave))
                    {
                        (ready ??= new List<KeyValuePair<int, IndividualRespawn.Waiter>>()).Add(pair);
                    }
                }

                if (ready == null)
                {
                    continue;
                }

                IndividualRespawn.Waiter.SortByWaitedLongest(ready);
                foreach (var pair in ready)
                {
                    if (session.State != GameModeState.Playing)
                    {
                        return;
                    }

                    if (_waiters.TryGetValue(pair.Key, out var current)
                        && ReferenceEquals(current, pair.Value)
                        && IsReadyAtWave(current, sinceWave))
                    {
                        _waiters.Remove(pair.Key);
                        respawn(pair.Key);
                    }
                }
            }
        }

        private static bool IsReadyAtWave(IndividualRespawn.Waiter waiter, float sinceWave)
        {
            return waiter.Remaining + sinceWave <= 0f;
        }

        private double WaveTime(long index)
        {
            return index * (double)WaveIntervalSeconds;
        }

        private long FirstWaveAtOrAfter(double readyAt)
        {
            var index = Math.Max(_wavesPassed + 1, (long)Math.Ceiling(readyAt / WaveIntervalSeconds));

            // 割り算の丸めで 1 つずれることがあるので、前後を確かめて合わせる。
            while (WaveTime(index) < readyAt)
            {
                index++;
            }

            while (index - 1 > _wavesPassed && WaveTime(index - 1) >= readyAt)
            {
                index--;
            }

            return index;
        }
    }
}

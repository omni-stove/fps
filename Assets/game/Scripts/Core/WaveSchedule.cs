using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // ハンドラの中で見える値が変わった後のものになるよう、イベントは状態を変え終えてから呼ぶ。
    public sealed class WaveSchedule
    {
        public event Action<int, Wave> WaveStarted;

        public event Action<int> WaveCleared;

        public event Action<int> RestEnded;

        public event Action Completed;

        private readonly Func<int, Wave> _waveAt;

        private WaveSchedule(Func<int, Wave> waveAt, int? waveCount)
        {
            _waveAt = waveAt;
            WaveCount = waveCount;
            Phase = WavePhase.Waiting;
        }

        public int? WaveCount { get; }

        public WavePhase Phase { get; private set; }

        public int WaveNumber { get; private set; }

        public Wave CurrentWave
        {
            get
            {
                if (WaveNumber == 0)
                {
                    throw new InvalidOperationException("the wave schedule has not started");
                }

                return _current;
            }
        }

        public int Spawned { get; private set; }

        public int Killed { get; private set; }

        public int Alive => Spawned - Killed;

        public int RemainingToSpawn => Phase == WavePhase.Fighting ? _current.EnemyCount - Spawned : 0;

        public int RemainingEnemies => Phase == WavePhase.Fighting ? _current.EnemyCount - Killed : 0;

        public int SpawnBudget
        {
            get
            {
                if (Phase != WavePhase.Fighting)
                {
                    return 0;
                }

                return Math.Max(0, Math.Min(_current.MaxAlive - Alive, RemainingToSpawn));
            }
        }

        public float RestRemaining { get; private set; }

        public int? NextWaveNumber
        {
            get
            {
                if (Phase == WavePhase.Completed)
                {
                    return null;
                }

                var next = WaveNumber + 1;
                return WaveCount.HasValue && next > WaveCount.Value ? (int?)null : next;
            }
        }

        private Wave _current;

        public static WaveSchedule FromTable(IReadOnlyList<Wave> waves)
        {
            if (waves == null)
            {
                throw new ArgumentNullException(nameof(waves));
            }

            if (waves.Count == 0)
            {
                throw new ArgumentException("waves must not be empty", nameof(waves));
            }

            var copy = new Wave[waves.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                if (waves[i].EnemyCount < 1)
                {
                    throw new ArgumentException($"wave {i + 1} has no enemies; build each wave with the Wave constructor", nameof(waves));
                }

                copy[i] = waves[i];
            }

            return new WaveSchedule(number => copy[number - 1], copy.Length);
        }

        public static WaveSchedule FromFormula(WaveFormula formula, int waveCount)
        {
            if (formula == null)
            {
                throw new ArgumentNullException(nameof(formula));
            }

            if (waveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveCount), waveCount, "waveCount must not be negative");
            }

            return new WaveSchedule(formula.At, waveCount == 0 ? (int?)null : waveCount);
        }

        public Wave WaveAt(int number)
        {
            if (number < 1 || (WaveCount.HasValue && number > WaveCount.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(number), number, "number must be between 1 and the wave count");
            }

            return _waveAt(number);
        }

        public void Start()
        {
            if (Phase != WavePhase.Waiting)
            {
                throw new InvalidOperationException("the wave schedule has already started");
            }

            BeginWave(1);
        }

        public void ReportSpawned()
        {
            if (SpawnBudget <= 0)
            {
                throw new InvalidOperationException($"no enemy may be spawned now (phase={Phase}, alive={Alive}, spawned={Spawned})");
            }

            Spawned++;
        }

        public void ReportKilled()
        {
            if (Phase != WavePhase.Fighting)
            {
                throw new InvalidOperationException($"enemies can only be killed while fighting, but the phase is {Phase}");
            }

            if (Alive <= 0)
            {
                throw new InvalidOperationException("there is no spawned enemy left to kill");
            }

            Killed++;
            if (Killed < _current.EnemyCount)
            {
                return;
            }

            var cleared = WaveNumber;
            var completed = WaveCount.HasValue && cleared >= WaveCount.Value;
            if (completed)
            {
                Phase = WavePhase.Completed;
                RestRemaining = 0f;
            }
            else
            {
                Phase = WavePhase.Resting;
                RestRemaining = _current.RestSeconds;
            }

            WaveCleared?.Invoke(cleared);
            if (completed)
            {
                Completed?.Invoke();
            }
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (Phase != WavePhase.Resting)
            {
                return;
            }

            RestRemaining = Math.Max(0f, RestRemaining - deltaSeconds);
            if (RestRemaining > 0f)
            {
                return;
            }

            BeginWave(WaveNumber + 1, raiseRestEnded: true);
        }

        private void BeginWave(int number, bool raiseRestEnded = false)
        {
            var wave = _waveAt(number);
            WaveNumber = number;
            _current = wave;
            Spawned = 0;
            Killed = 0;
            RestRemaining = 0f;
            Phase = WavePhase.Fighting;

            if (raiseRestEnded)
            {
                RestEnded?.Invoke(number);
            }

            // RestEnded のハンドラが波を進めていたら (報告や Tick を呼んだなど)、古い番号で始まりを知らせない。
            if (WaveNumber == number && Phase == WavePhase.Fighting)
            {
                WaveStarted?.Invoke(number, wave);
            }
        }
    }
}

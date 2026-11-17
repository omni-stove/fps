using System;

namespace Shooter.Core
{
    // 息を少しずつ戻さず顔を出したら満タンにするのは、戻る速さという調整値を増やさずに、息継ぎで必ず助かるようにするため。
    public sealed class DrowningClock
    {
        private SwimTuning _tuning;
        private float _submergedSeconds;
        private long _hits;

        public DrowningClock(SwimTuning tuning)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        public SwimTuning Tuning
        {
            get => _tuning;
            set
            {
                _tuning = value ?? throw new ArgumentNullException(nameof(value));
                // 数え済みの回数を新しい調整値の上限に丸めるのは、差し替えで Tick が負のダメージを返さないようにするため。
                _hits = Math.Min(_hits, DueHits());
            }
        }

        public float SubmergedSeconds => _submergedSeconds;

        public float BreathLeft => Math.Max(0f, _tuning.BreathSeconds - _submergedSeconds);

        public float BreathFraction => BreathLeft / _tuning.BreathSeconds;

        public bool IsOutOfBreath => _submergedSeconds >= _tuning.BreathSeconds;

        public int Tick(bool submerged, float seconds)
        {
            if (!(seconds >= 0f) || float.IsInfinity(seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(seconds), seconds, "seconds must be finite and not negative");
            }

            if (!submerged)
            {
                Reset();
                return 0;
            }

            _submergedSeconds += seconds;
            var due = DueHits();
            if (due == 0 || _tuning.DrownDamage == 0)
            {
                return 0;
            }

            var hits = Math.Max(0L, due - _hits);
            _hits = Math.Max(_hits, due);

            // 1 フレームが極端に長くても、体力の型を超えるダメージの合計で桁あふれさせない。
            return (int)Math.Min(int.MaxValue, hits * _tuning.DrownDamage);
        }

        private long DueHits()
        {
            var over = _submergedSeconds - _tuning.BreathSeconds;
            return over < 0f ? 0L : (long)Math.Floor(over / _tuning.DrownIntervalSeconds) + 1;
        }

        public void Reset()
        {
            _submergedSeconds = 0f;
            _hits = 0;
        }
    }
}

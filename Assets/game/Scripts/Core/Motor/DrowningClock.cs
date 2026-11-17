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
            set => _tuning = value ?? throw new ArgumentNullException(nameof(value));
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
            var over = _submergedSeconds - _tuning.BreathSeconds;
            if (over < 0f || _tuning.DrownDamage == 0)
            {
                return 0;
            }

            var due = (long)Math.Floor(over / _tuning.DrownIntervalSeconds) + 1;
            var hits = due - _hits;
            _hits = due;

            // 1 フレームが極端に長くても、体力の型を超えるダメージの合計で桁あふれさせない。
            return (int)Math.Min(int.MaxValue, hits * _tuning.DrownDamage);
        }

        public void Reset()
        {
            _submergedSeconds = 0f;
            _hits = 0;
        }
    }
}

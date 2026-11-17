using System;

namespace Shooter.Core
{
    // 届く間隔が揺れて数発がまとめて届いても受け入れ、間隔より速く撃ち続ける不正は止めるよう、
    // 間隔ごとに 1 発ぶん回復して Burst 発まで溜まる形で数える。
    public sealed class ShotBudget
    {
        public const int DefaultBurst = 3;

        private double _tokens;
        private double _lastSeconds;
        private bool _started;

        public float MinShotSpacingSeconds { get; }

        public int Burst { get; }

        // 連射間隔でなく撃ち方の最短の間隔で回復させるのは、バーストのように間隔より速く続けて撃つ撃ち方を弾かないため。
        public ShotBudget(ITriggerModel trigger, int burst = DefaultBurst)
            : this((trigger ?? throw new ArgumentNullException(nameof(trigger))).MinShotSpacingSeconds, burst)
        {
        }

        public ShotBudget(float minShotSpacingSeconds, int burst = DefaultBurst)
        {
            if (float.IsNaN(minShotSpacingSeconds) || float.IsInfinity(minShotSpacingSeconds) || minShotSpacingSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(minShotSpacingSeconds), minShotSpacingSeconds, "minShotSpacingSeconds must be finite and not negative");
            }

            if (burst <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(burst), burst, "burst must be positive");
            }

            MinShotSpacingSeconds = minShotSpacingSeconds;
            Burst = burst;
            _tokens = burst;
        }

        public bool TryConsume(double nowSeconds)
        {
            if (double.IsNaN(nowSeconds) || double.IsInfinity(nowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(nowSeconds), nowSeconds, "nowSeconds must be finite");
            }

            if (MinShotSpacingSeconds == 0f)
            {
                return true;
            }

            if (_started && nowSeconds > _lastSeconds)
            {
                _tokens = Math.Min(Burst, _tokens + ((nowSeconds - _lastSeconds) / MinShotSpacingSeconds));
            }

            if (!_started || nowSeconds > _lastSeconds)
            {
                _lastSeconds = nowSeconds;
                _started = true;
            }

            // 間隔ちょうどで撃ったとき、浮動小数の誤差で 1 にわずかに届かず弾かれないよう少しだけ緩める
            if (_tokens < 1d - 1e-6)
            {
                return false;
            }

            _tokens = Math.Max(0d, _tokens - 1d);
            return true;
        }
    }
}

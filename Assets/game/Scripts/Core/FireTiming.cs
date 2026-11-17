using System;

namespace Shooter.Core
{
    public static class FireTiming
    {
        private const float _maxAlpha = 0.99999994f;

        public static int TickAt(double timeSeconds, int tickRate)
        {
            ValidateTickRate(tickRate);
            if (double.IsNaN(timeSeconds) || double.IsInfinity(timeSeconds) || timeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds, "timeSeconds must be finite and not negative");
            }

            var ticks = Math.Floor(timeSeconds * tickRate);
            if (ticks > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(timeSeconds), timeSeconds, "timeSeconds is too large to fit a tick");
            }

            return (int)ticks;
        }

        public static ShotTime ViewTime(double serverTimeSeconds, int tickRate, int interpolationDelayTicks)
        {
            ValidateTickRate(tickRate);
            if (double.IsNaN(serverTimeSeconds) || double.IsInfinity(serverTimeSeconds) || serverTimeSeconds < 0d)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTimeSeconds), serverTimeSeconds, "serverTimeSeconds must be finite and not negative");
            }

            if (interpolationDelayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(interpolationDelayTicks), interpolationDelayTicks, "interpolationDelayTicks must not be negative");
            }

            var view = Math.Max(0d, (serverTimeSeconds * tickRate) - interpolationDelayTicks);
            var tick = Math.Floor(view);
            if (tick > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(serverTimeSeconds), serverTimeSeconds, "serverTimeSeconds is too large to fit a tick");
            }

            // double では 1 未満でも float に丸めると 1 になることがあるので、1 未満に収める
            var alpha = (float)(view - tick);
            return new ShotTime((int)tick, alpha < 1f ? alpha : _maxAlpha);
        }

        private static void ValidateTickRate(int tickRate)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be positive");
            }
        }
    }
}

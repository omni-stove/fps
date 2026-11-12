using System;

namespace Fps.Core
{
    /// <summary>
    /// ラグ補償に使う時刻の換算。サーバーの時刻 (秒) を tick に直し、撃った側が見ていた時点 (<see cref="ShotTime"/>) を求める。
    /// </summary>
    public static class FireTiming
    {
        // 1 未満で最大の float。
        private const float _maxAlpha = 0.99999994f;

        /// <summary>
        /// timeSeconds 時点の tick (切り捨て)。サーバーが位置を <see cref="LagCompensator.Record"/> するときの tick に使う。
        /// </summary>
        /// <param name="timeSeconds">時刻 (秒)。0 以上の有限値。</param>
        /// <param name="tickRate">1 秒あたりの tick 数。1 以上。</param>
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

        /// <summary>
        /// クライアントが見ている他人の位置の時点。サーバーから届いている時刻 serverTimeSeconds から
        /// 補間の遅れ interpolationDelayTicks を引き、tick と割合に分ける。0 より前にはならない。
        /// </summary>
        /// <param name="serverTimeSeconds">クライアントが受け取っているサーバーの時刻 (秒)。0 以上の有限値。</param>
        /// <param name="tickRate">1 秒あたりの tick 数。1 以上。</param>
        /// <param name="interpolationDelayTicks">他人を何 tick 遅らせて表示しているか。0 以上。</param>
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

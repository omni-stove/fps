using System;

namespace Fps.Core
{
    /// <summary>
    /// 固定 tick クロック。可変のフレーム時間を受け取り、固定間隔の tick に変換する。
    /// ネットワーク同期とクライアント予測の基準にする。時間は外から渡す。
    /// </summary>
    public sealed class TickClock
    {
        /// <summary>既定の tick レート（Hz）。</summary>
        public const int DefaultTickRate = 60;

        /// <summary>1 回の Advance で進める tick 数の既定上限。</summary>
        public const int DefaultMaxTicksPerAdvance = 8;

        // float の delta は 1/60 などを正確に表せず、合計がわずかに境界を下回ることがある。
        // それで tick を 1 つ取りこぼさないための許容幅（tick 単位。60Hz で約 1.7 マイクロ秒）。
        private const double _boundaryEpsilon = 1e-4;

        // 1 未満で最大の float。
        private const float _maxAlpha = 0.99999994f;

        // 次の tick までに溜まった時間。誤差が溜まらないよう tick 単位の double で持つ。
        private double _pendingTicks;

        /// <summary>1 秒あたりの tick 数。</summary>
        public int TickRate { get; }

        /// <summary>1 tick の長さ（秒）。</summary>
        public float TickSeconds { get; }

        /// <summary>1 回の Advance で進める tick 数の上限。超えた分は捨てる。</summary>
        public int MaxTicksPerAdvance { get; }

        /// <summary>これまでに進んだ tick 数。0 始まり。</summary>
        public int CurrentTick { get; private set; }

        /// <summary>次の tick までの割合。0 以上 1 未満。描画の補間に使う。</summary>
        public float Alpha
        {
            get
            {
                var alpha = (float)_pendingTicks;
                if (alpha < 0f)
                {
                    return 0f;
                }

                // double では 1 未満でも float に丸めると 1 になることがあるので、1 未満に収める。
                return alpha < 1f ? alpha : _maxAlpha;
            }
        }

        /// <param name="tickRate">1 秒あたりの tick 数。1 以上。</param>
        /// <param name="maxTicksPerAdvance">1 回の Advance で進める tick 数の上限。1 以上。</param>
        public TickClock(int tickRate = DefaultTickRate, int maxTicksPerAdvance = DefaultMaxTicksPerAdvance)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tickRate must be positive");
            }

            if (maxTicksPerAdvance <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxTicksPerAdvance), maxTicksPerAdvance, "maxTicksPerAdvance must be positive");
            }

            TickRate = tickRate;
            TickSeconds = 1f / tickRate;
            MaxTicksPerAdvance = maxTicksPerAdvance;
        }

        /// <summary>
        /// 経過時間を足し、今回進めるべき tick 数を返して CurrentTick を進める。
        /// 余りは次回に持ち越す。MaxTicksPerAdvance を超えた分は捨てる。
        /// </summary>
        /// <param name="deltaSeconds">前回からの経過時間（秒）。0 以上の有限値。</param>
        /// <returns>今回進めた tick 数。0 以上 MaxTicksPerAdvance 以下。</returns>
        public int Advance(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a finite, non-negative value");
            }

            _pendingTicks += (double)deltaSeconds * TickRate;

            var wholeTicks = Math.Floor(_pendingTicks + _boundaryEpsilon);
            if (wholeTicks <= 0d)
            {
                return 0;
            }

            // 端数は持ち越し、整数部はすべて消費する。上限を超えた分はここで捨てる。
            _pendingTicks -= wholeTicks;
            var ticks = wholeTicks > MaxTicksPerAdvance ? MaxTicksPerAdvance : (int)wholeTicks;
            CurrentTick += ticks;
            return ticks;
        }
    }
}

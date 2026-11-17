using System;

namespace Shooter.Core
{
    public sealed class TickClock
    {
        public const int DefaultTickRate = 60;

        public const int DefaultMaxTicksPerAdvance = 8;

        // float の delta は 1/60 などを正確に表せず、合計がわずかに境界を下回ることがある。
        // それで tick を 1 つ取りこぼさないための許容幅（tick 単位。60Hz で約 1.7 マイクロ秒）。
        private const double _boundaryEpsilon = 1e-4;

        // 1 未満で最大の float。
        private const float _maxAlpha = 0.99999994f;

        // 次の tick までに溜まった時間。誤差が溜まらないよう tick 単位の double で持つ。
        private double _pendingTicks;

        public int TickRate { get; }

        public float TickSeconds { get; }

        public int MaxTicksPerAdvance { get; }

        public int CurrentTick { get; private set; }

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

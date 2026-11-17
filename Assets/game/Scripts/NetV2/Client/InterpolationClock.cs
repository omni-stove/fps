using System;

namespace Shooter.NetV2
{
    // 届いた tick へ飛ばずフレームの時間で少しずつ寄せるのは、届き方の揺れで表示が止まったり跳んだりしないため。
    // 寄せ方の値は Netcode のクライアント (NetworkPlayerMotor の補間) と同じにする。
    public sealed class InterpolationClock
    {
        // Netcode のクライアント (NetworkPlayerMotor.InterpolationDelayTicks) と同じ遅れ。
        public const float DefaultDelayTicks = 2f;

        private const float _catchUpGain = 0.1f;
        private const float _maxCatchUp = 0.2f;

        private readonly float _maxDriftTicks;

        public InterpolationClock(int tickRate, float delayTicks = DefaultDelayTicks)
        {
            if (tickRate <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "tick rate must be positive");
            }

            if (!(delayTicks >= 0f) || float.IsInfinity(delayTicks))
            {
                throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks, "delay must be zero or more");
            }

            TickRate = tickRate;
            DelayTicks = delayTicks;
            _maxDriftTicks = tickRate * 0.5f;
        }

        public int TickRate { get; }

        public float DelayTicks { get; }

        public bool HasStarted { get; private set; }

        public float RenderTick { get; private set; }

        public void Advance(float deltaSeconds, int newestTick)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "delta must be zero or more");
            }

            var target = newestTick - DelayTicks;
            if (!HasStarted || Math.Abs(target - RenderTick) > _maxDriftTicks)
            {
                HasStarted = true;
                RenderTick = target;
                return;
            }

            var error = target - RenderTick;
            var rate = 1f + Clamp(error * _catchUpGain, -_maxCatchUp, _maxCatchUp);
            RenderTick = Math.Min(RenderTick + deltaSeconds * TickRate * rate, newestTick);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}

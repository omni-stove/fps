using System;

namespace Fps.NetV2
{
    /// <summary>
    /// 他のプレイヤーを表示するサーバーの tick (小数)。届いた最新の tick から <see cref="DelayTicks"/> 遅らせた所を目指し、
    /// フレームの時間で進めながら少しずつ寄せる。届き方の揺れで表示が止まったり跳んだりしないため。
    /// 長い途切れの後などで目標から大きく離れたら、寄せずに目標へ飛ぶ。
    /// </summary>
    /// <remarks>寄せ方の値は Netcode のクライアント (<c>NetworkPlayerMotor</c> の補間) と同じにする。</remarks>
    public sealed class InterpolationClock
    {
        /// <summary>Netcode のクライアント (<c>NetworkPlayerMotor.InterpolationDelayTicks</c>) と同じ遅れ。</summary>
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

        /// <summary>最初の tick が届いて進み始めたか。</summary>
        public bool HasStarted { get; private set; }

        /// <summary>今表示するサーバーの tick。</summary>
        public float RenderTick { get; private set; }

        /// <summary>newestTick (届いた最新の tick) を目指して deltaSeconds だけ進める。</summary>
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

using System;

namespace Fps.Core
{
    /// <summary>
    /// 死亡から復活までの待ち時間。<see cref="Start"/> で数え始め、<see cref="Tick"/> に経過時間を渡すと、
    /// 待ち時間を過ぎた回だけ true を返す。時間は外から渡す。
    /// </summary>
    public sealed class RespawnCountdown
    {
        /// <summary>復活までの待ち時間 (秒)。</summary>
        public float DelaySeconds { get; }

        /// <summary>数えている途中なら true。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>復活までの残り時間 (秒)。数えていなければ 0。</summary>
        public float RemainingSeconds { get; private set; }

        /// <param name="delaySeconds">復活までの待ち時間 (秒)。0 以上の有限値。</param>
        public RespawnCountdown(float delaySeconds)
        {
            if (!(delaySeconds >= 0f) || float.IsInfinity(delaySeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(delaySeconds), delaySeconds, "delaySeconds must be a finite, non-negative value");
            }

            DelaySeconds = delaySeconds;
        }

        /// <summary>待ち時間を最初から数え直す。数えている途中で呼んでも最初からになる。</summary>
        public void Start()
        {
            IsRunning = true;
            RemainingSeconds = DelaySeconds;
        }

        /// <summary>数えるのをやめる。<see cref="Tick"/> は次の <see cref="Start"/> まで true を返さない。</summary>
        public void Cancel()
        {
            IsRunning = false;
            RemainingSeconds = 0f;
        }

        /// <summary>
        /// 経過時間を進める。待ち時間を過ぎた回だけ true を返して止まる。数えていなければ何もせず false。
        /// </summary>
        /// <param name="deltaSeconds">前回からの経過時間 (秒)。0 以上の有限値。</param>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a finite, non-negative value");
            }

            if (!IsRunning)
            {
                return false;
            }

            RemainingSeconds -= deltaSeconds;
            if (RemainingSeconds > 0f)
            {
                return false;
            }

            Cancel();
            return true;
        }
    }
}

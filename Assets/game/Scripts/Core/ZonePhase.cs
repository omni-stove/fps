using System;

namespace Fps.Core
{
    /// <summary>
    /// 縮むエリア (<see cref="ZoneSchedule"/>) の段階 1 つ。<see cref="WaitSeconds"/> だけ今の円のまま待ち、
    /// <see cref="ShrinkSeconds"/> かけて半径 <see cref="Radius"/> の円へ縮む。この段階の間 (待ちと縮みの両方)、
    /// 円の外にいる者は毎秒 <see cref="DamagePerSecond"/> のダメージを受ける。
    /// </summary>
    public readonly struct ZonePhase
    {
        /// <summary>縮み始めるまでの待ち時間 (秒)。0 以上の有限値。</summary>
        public float WaitSeconds { get; }

        /// <summary>縮むのにかける時間 (秒)。0 以上の有限値。0 なら待ちの終わりに一度に縮む。</summary>
        public float ShrinkSeconds { get; }

        /// <summary>この段階の終わりの円の半径 (m)。0 より大きい有限値。</summary>
        public float Radius { get; }

        /// <summary>この段階の間、円の外で受けるダメージ (毎秒)。0 以上の有限値。</summary>
        public float DamagePerSecond { get; }

        /// <summary>待ちと縮みを合わせた、この段階の長さ (秒)。</summary>
        public float DurationSeconds => WaitSeconds + ShrinkSeconds;

        /// <param name="waitSeconds">縮み始めるまでの待ち時間 (秒)。0 以上の有限値。</param>
        /// <param name="shrinkSeconds">縮むのにかける時間 (秒)。0 以上の有限値。</param>
        /// <param name="radius">この段階の終わりの円の半径 (m)。0 より大きい有限値。</param>
        /// <param name="damagePerSecond">この段階の間、円の外で受けるダメージ (毎秒)。0 以上の有限値。</param>
        public ZonePhase(float waitSeconds, float shrinkSeconds, float radius, float damagePerSecond)
        {
            if (!(waitSeconds >= 0f) || float.IsInfinity(waitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(waitSeconds), waitSeconds, "waitSeconds must be a non-negative finite number");
            }

            if (!(shrinkSeconds >= 0f) || float.IsInfinity(shrinkSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(shrinkSeconds), shrinkSeconds, "shrinkSeconds must be a non-negative finite number");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be positive and finite");
            }

            if (!(damagePerSecond >= 0f) || float.IsInfinity(damagePerSecond))
            {
                throw new ArgumentOutOfRangeException(nameof(damagePerSecond), damagePerSecond, "damagePerSecond must be a non-negative finite number");
            }

            WaitSeconds = waitSeconds;
            ShrinkSeconds = shrinkSeconds;
            Radius = radius;
            DamagePerSecond = damagePerSecond;
        }
    }
}

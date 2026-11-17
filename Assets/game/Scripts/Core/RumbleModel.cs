using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>ゲームパッドの 2 つのモーターに入れる強さ (0〜1)。</summary>
    public readonly struct RumbleMotors : IEquatable<RumbleMotors>
    {
        public static RumbleMotors Zero => default;

        public float Low { get; }

        public float High { get; }

        public bool IsZero => Low <= 0f && High <= 0f;

        public RumbleMotors(float low, float high)
        {
            Low = low;
            High = high;
        }

        public bool Equals(RumbleMotors other)
        {
            return Low.Equals(other.Low) && High.Equals(other.High);
        }

        public override bool Equals(object obj)
        {
            return obj is RumbleMotors other && Equals(other);
        }

        public static bool operator ==(RumbleMotors left, RumbleMotors right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(RumbleMotors left, RumbleMotors right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Low.GetHashCode() * 397) ^ High.GetHashCode();
            }
        }
    }

    /// <summary>
    /// 受けた振動 (<see cref="RumblePulse"/>) を秒数のあいだ持ち、今モーターに入れる強さを出す。
    /// 重なっている間はモーターごとに強い方を取る (足さない)。設定のオンオフと強さの倍率は <see cref="Sample"/> で掛ける。
    /// </summary>
    public sealed class RumbleModel
    {
        private readonly List<Active> _active = new List<Active>();

        /// <summary>まだ続いている振動の数。</summary>
        public int ActiveCount => _active.Count;

        /// <summary>設定を掛ける前の、モーターごとに一番強い値。</summary>
        public RumbleMotors Current
        {
            get
            {
                var low = 0f;
                var high = 0f;
                foreach (var active in _active)
                {
                    low = Math.Max(low, active.Pulse.Low);
                    high = Math.Max(high, active.Pulse.High);
                }

                return new RumbleMotors(low, high);
            }
        }

        /// <summary>
        /// 中心から distance 離れた爆発の振動。強さは中心で pulse のまま、radius で 0 になるよう距離に比例して減る。秒数は変えない。
        /// radius 以上離れていれば <see cref="RumblePulse.None"/>。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">distance が負か有限でない、radius が 0 以下か有限でない。</exception>
        public static RumblePulse ForExplosion(RumblePulse pulse, float distance, float radius)
        {
            if (!(distance >= 0f) || float.IsInfinity(distance))
            {
                throw new ArgumentOutOfRangeException(nameof(distance), distance, "distance must be a non-negative finite number");
            }

            if (!(radius > 0f) || float.IsInfinity(radius))
            {
                throw new ArgumentOutOfRangeException(nameof(radius), radius, "radius must be a positive finite number");
            }

            if (distance >= radius)
            {
                return RumblePulse.None;
            }

            var factor = 1f - (distance / radius);
            return new RumblePulse(pulse.Low * factor, pulse.High * factor, pulse.Seconds);
        }

        /// <summary>
        /// 設定を掛けた強さ。enabled が false なら 0、そうでなければ raw の両方のモーターに strength (0〜1) を掛ける。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">strength が 0〜1 でない。</exception>
        public static RumbleMotors Apply(RumbleMotors raw, bool enabled, float strength)
        {
            if (!(strength >= 0f && strength <= 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(strength), strength, "strength must be between 0 and 1");
            }

            if (!enabled || strength <= 0f)
            {
                return RumbleMotors.Zero;
            }

            return new RumbleMotors(raw.Low * strength, raw.High * strength);
        }

        /// <summary>振動を足す。何も回さない振動 (<see cref="RumblePulse.IsSilent"/>) は持たず false を返す。</summary>
        public bool Add(RumblePulse pulse)
        {
            if (pulse.IsSilent)
            {
                return false;
            }

            _active.Add(new Active(pulse, pulse.Seconds));
            return true;
        }

        /// <summary>
        /// deltaSeconds だけ時間を進め、秒数を使い切った振動を捨てる。使い切った振動が最後の 1 つなら true
        /// (モーターを止める合図)。
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">deltaSeconds が負か有限でない。</exception>
        public bool Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            if (_active.Count == 0)
            {
                return false;
            }

            for (var i = _active.Count - 1; i >= 0; i--)
            {
                var remaining = _active[i].Remaining - deltaSeconds;
                if (remaining <= 0f)
                {
                    _active.RemoveAt(i);
                }
                else
                {
                    _active[i] = new Active(_active[i].Pulse, remaining);
                }
            }

            return _active.Count == 0;
        }

        /// <summary>今の強さ (<see cref="Current"/>) に設定を掛けたもの (<see cref="Apply"/>)。</summary>
        /// <exception cref="ArgumentOutOfRangeException">strength が 0〜1 でない。</exception>
        public RumbleMotors Sample(bool enabled, float strength)
        {
            return Apply(Current, enabled, strength);
        }

        /// <summary>続いている振動をすべて捨てる。</summary>
        public void Reset()
        {
            _active.Clear();
        }

        private readonly struct Active
        {
            public RumblePulse Pulse { get; }

            public float Remaining { get; }

            public Active(RumblePulse pulse, float remaining)
            {
                Pulse = pulse;
                Remaining = remaining;
            }
        }
    }
}

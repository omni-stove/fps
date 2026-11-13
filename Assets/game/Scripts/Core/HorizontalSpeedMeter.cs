using System;

namespace Fps.Core
{
    /// <summary>
    /// 位置の変化から水平の移動の速さ (m/s) を求める。速度そのものが同期されないキャラ (ネット対戦の他人のプレイヤーなど) の
    /// 見た目に使う。
    /// <list type="bullet">
    /// <item>速さは <see cref="WindowSeconds"/> 秒ぶんの移動をまとめて割った値で、窓が埋まるたびに更新する。
    /// 位置が tick ごとにしか動かないキャラを毎フレーム測ると、動いたフレームと止まったフレームで速さが跳ねるため。</item>
    /// <item>1 回で <see cref="MaxSpeed"/> × <see cref="WindowSeconds"/> m を超えて動いたら瞬間移動 (リスポーンなど) と見なし、
    /// その移動は数えずに速さを 0 に戻して測り直す。</item>
    /// </list>
    /// </summary>
    public sealed class HorizontalSpeedMeter
    {
        /// <summary>速さをまとめて求める秒数の既定値。60Hz の tick なら 6 tick ぶん。</summary>
        public const float DefaultWindowSeconds = 0.1f;

        /// <summary>これより速い移動を瞬間移動と見なす速さ (m/s) の既定値。ダッシュより十分速い。</summary>
        public const float DefaultMaxSpeed = 30f;

        private float _lastX;
        private float _lastZ;
        private double _distance;
        private double _elapsed;

        /// <param name="windowSeconds">速さをまとめて求める秒数。0 より大きい有限値。</param>
        /// <param name="maxSpeed">これより速い移動を瞬間移動と見なす速さ (m/s)。0 より大きい有限値。</param>
        public HorizontalSpeedMeter(float windowSeconds = DefaultWindowSeconds, float maxSpeed = DefaultMaxSpeed)
        {
            if (!(windowSeconds > 0f) || float.IsInfinity(windowSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(windowSeconds), windowSeconds, "windowSeconds must be positive and finite");
            }

            if (!(maxSpeed > 0f) || float.IsInfinity(maxSpeed))
            {
                throw new ArgumentOutOfRangeException(nameof(maxSpeed), maxSpeed, "maxSpeed must be positive and finite");
            }

            WindowSeconds = windowSeconds;
            MaxSpeed = maxSpeed;
        }

        public float WindowSeconds { get; }

        public float MaxSpeed { get; }

        /// <summary>最後に求めた水平の速さ (m/s)。0 以上の有限値。まだ求めていなければ 0。</summary>
        public float Speed { get; private set; }

        /// <summary>位置を 1 度でも受け取ったか。</summary>
        public bool HasPosition { get; private set; }

        /// <summary>
        /// 前回から deltaSeconds 秒たった今の水平の位置 (x, z) を渡し、<see cref="Speed"/> を返す。
        /// 最初の 1 回は位置を覚えるだけで 0 を返す。
        /// </summary>
        /// <param name="x">今の X 座標。有限値。</param>
        /// <param name="z">今の Z 座標。有限値。</param>
        /// <param name="deltaSeconds">前回からの秒数。0 以上の有限値。</param>
        public float Sample(float x, float z, float deltaSeconds)
        {
            if (float.IsNaN(x) || float.IsInfinity(x))
            {
                throw new ArgumentOutOfRangeException(nameof(x), x, "x must be finite");
            }

            if (float.IsNaN(z) || float.IsInfinity(z))
            {
                throw new ArgumentOutOfRangeException(nameof(z), z, "z must be finite");
            }

            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!HasPosition)
            {
                Remember(x, z);
                HasPosition = true;
                return Speed;
            }

            var dx = (double)x - _lastX;
            var dz = (double)z - _lastZ;
            var step = Math.Sqrt((dx * dx) + (dz * dz));
            Remember(x, z);

            if (step > (double)MaxSpeed * WindowSeconds)
            {
                _distance = 0d;
                _elapsed = 0d;
                Speed = 0f;
                return Speed;
            }

            _distance += step;
            _elapsed += deltaSeconds;
            if (_elapsed >= WindowSeconds)
            {
                Speed = (float)Math.Min(_distance / _elapsed, MaxSpeed);
                _distance = 0d;
                _elapsed = 0d;
            }

            return Speed;
        }

        /// <summary>覚えた位置と速さを捨てる。次の <see cref="Sample"/> は位置を覚えるだけになる。</summary>
        public void Reset()
        {
            HasPosition = false;
            Speed = 0f;
            _distance = 0d;
            _elapsed = 0d;
        }

        private void Remember(float x, float z)
        {
            _lastX = x;
            _lastZ = z;
        }
    }
}

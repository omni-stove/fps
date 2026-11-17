using System;

namespace Shooter.Core
{
    public sealed class HorizontalSpeedMeter
    {
        // 毎フレーム測らず窓でまとめるのは、位置が tick ごとにしか動かないキャラを毎フレーム測ると、動いたフレームと止まったフレームで速さが跳ねるため。
        public const float DefaultWindowSeconds = 0.1f;

        // 瞬間移動と取り違えないよう、ダッシュより十分速くしてある。
        public const float DefaultMaxSpeed = 30f;

        private float _lastX;
        private float _lastZ;
        private double _distance;
        private double _elapsed;

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

        public float Speed { get; private set; }

        public bool HasPosition { get; private set; }

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

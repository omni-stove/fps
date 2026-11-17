using System;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class ProneDive
    {
        private Vector2 _direction;
        private float _elapsed;
        private bool _isDiving;

        public ProneDive(ProneSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            Distance = spec.DiveDistance;
            Seconds = spec.DiveSeconds;
        }

        public float Distance { get; }

        public float Seconds { get; }

        public bool IsDiving => _isDiving;

        public Vector2 Direction => _direction;

        public float Elapsed => _isDiving ? _elapsed : 0f;

        public Vector2 Velocity => _isDiving ? _direction * (2f * Distance / Seconds * (1f - (_elapsed / Seconds))) : Vector2.Zero;

        public bool TryStart(Vector2 direction)
        {
            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (_isDiving)
            {
                return false;
            }

            _direction = Vector2.Normalize(direction);
            _elapsed = 0f;
            _isDiving = true;
            return true;
        }

        // 合計をフレームの刻みによらず Distance にするため、終わる Step では残りの距離ちょうどを返す。
        public Vector2 Step(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (!_isDiving)
            {
                return Vector2.Zero;
            }

            var before = _elapsed;
            var after = Math.Min(Seconds, before + deltaSeconds);
            _elapsed = after;

            // 進んだ距離の割合 (速さを一定の割合で落とすので 2u - u²) の差で出し、最後に始点からちょうど Distance の所で止める。
            var displacement = _direction * (Distance * (Travelled(after / Seconds) - Travelled(before / Seconds)));
            if (after >= Seconds)
            {
                Finish();
            }

            return displacement;
        }

        public void Restore(bool diving, Vector2 direction, float elapsed)
        {
            if (!diving)
            {
                Finish();
                return;
            }

            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || direction.LengthSquared() <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be finite and not zero");
            }

            if (!(elapsed >= 0f && elapsed < Seconds))
            {
                throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "elapsed must be in [0, Seconds)");
            }

            _direction = direction;
            _elapsed = elapsed;
            _isDiving = true;
        }

        public bool Cancel()
        {
            if (!_isDiving)
            {
                return false;
            }

            Finish();
            return true;
        }

        private static float Travelled(float fraction)
        {
            return (2f * fraction) - (fraction * fraction);
        }

        private void Finish()
        {
            _isDiving = false;
            _elapsed = 0f;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

using System;
using System.Numerics;

namespace Shooter.Core
{
    // SlideMotion を持ち主ごとに持たせず状態に載せるのは、予測の巻き戻しでもサーバーと同じ速さと経過から滑り直せるようにするため。
    public readonly struct CharacterSlideState : IEquatable<CharacterSlideState>
    {
        private CharacterSlideState(Vector2 direction, float speed, float elapsed, float distance, float airborneSeconds)
        {
            IsSliding = true;
            Direction = direction;
            Speed = speed;
            Elapsed = elapsed;
            Distance = distance;
            AirborneSeconds = airborneSeconds;
        }

        public static CharacterSlideState None => default;

        public bool IsSliding { get; }

        // 水平の向き (x, z) で、長さ 1
        public Vector2 Direction { get; }

        public float Speed { get; }

        public float Elapsed { get; }

        public float Distance { get; }

        // 接地が外れてからの秒数。SlideStep.AirborneGraceSeconds に達すると終わる
        public float AirborneSeconds { get; }

        public bool IsValid => IsSliding
            ? IsFinite(Direction.X) && IsFinite(Direction.Y) && Direction.LengthSquared() > 0f
                && IsNonNegative(Speed) && IsNonNegative(Elapsed) && IsNonNegative(Distance) && IsNonNegative(AirborneSeconds)
            : Direction == Vector2.Zero && Speed == 0f && Elapsed == 0f && Distance == 0f && AirborneSeconds == 0f;

        public static CharacterSlideState Sliding(Vector2 direction, float speed, float elapsed, float distance, float airborneSeconds)
        {
            if (!IsFinite(direction.X) || !IsFinite(direction.Y) || !(direction.LengthSquared() > 0f))
            {
                throw new ArgumentException($"direction must be finite and not zero: {direction}", nameof(direction));
            }

            RequireNonNegative(speed, nameof(speed));
            RequireNonNegative(elapsed, nameof(elapsed));
            RequireNonNegative(distance, nameof(distance));
            RequireNonNegative(airborneSeconds, nameof(airborneSeconds));
            return new CharacterSlideState(Vector2.Normalize(direction), speed, elapsed, distance, airborneSeconds);
        }

        public bool Equals(CharacterSlideState other)
        {
            return IsSliding == other.IsSliding
                && Direction.Equals(other.Direction)
                && Speed.Equals(other.Speed)
                && Elapsed.Equals(other.Elapsed)
                && Distance.Equals(other.Distance)
                && AirborneSeconds.Equals(other.AirborneSeconds);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterSlideState other && Equals(other);
        }

        public static bool operator ==(CharacterSlideState left, CharacterSlideState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterSlideState left, CharacterSlideState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IsSliding ? 1 : 0) * 397) ^ Direction.GetHashCode() ^ (Speed.GetHashCode() << 1) ^ (Elapsed.GetHashCode() << 2)
                    ^ (Distance.GetHashCode() << 3) ^ (AirborneSeconds.GetHashCode() << 4);
            }
        }

        private static void RequireNonNegative(float value, string name)
        {
            if (!IsNonNegative(value))
            {
                throw new ArgumentOutOfRangeException(name, value, $"{name} must be finite and not negative");
            }
        }

        private static bool IsNonNegative(float value)
        {
            return value >= 0f && !float.IsInfinity(value);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

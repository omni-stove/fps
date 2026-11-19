using System;
using System.Numerics;

namespace Shooter.Core
{
    // ParachuteMotion を持ち主ごとに持たせず状態に載せるのは、予測の巻き戻しでもサーバーと同じ横の速さから進め直せるようにするため。
    public readonly struct CharacterParachuteState : IEquatable<CharacterParachuteState>
    {
        private CharacterParachuteState(Vector2 horizontalVelocity)
        {
            IsOpen = true;
            HorizontalVelocity = horizontalVelocity;
        }

        public static CharacterParachuteState None => default;

        public bool IsOpen { get; }

        public Vector2 HorizontalVelocity { get; }

        public bool IsValid => IsOpen
            ? IsFinite(HorizontalVelocity.X) && IsFinite(HorizontalVelocity.Y)
            : HorizontalVelocity == Vector2.Zero;

        public static CharacterParachuteState Open(Vector2 horizontalVelocity)
        {
            if (!IsFinite(horizontalVelocity.X) || !IsFinite(horizontalVelocity.Y))
            {
                throw new ArgumentException($"horizontalVelocity must be finite: {horizontalVelocity}", nameof(horizontalVelocity));
            }

            return new CharacterParachuteState(horizontalVelocity);
        }

        public bool Equals(CharacterParachuteState other)
        {
            return IsOpen == other.IsOpen && HorizontalVelocity.Equals(other.HorizontalVelocity);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterParachuteState other && Equals(other);
        }

        public static bool operator ==(CharacterParachuteState left, CharacterParachuteState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterParachuteState left, CharacterParachuteState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((IsOpen ? 1 : 0) * 397) ^ HorizontalVelocity.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

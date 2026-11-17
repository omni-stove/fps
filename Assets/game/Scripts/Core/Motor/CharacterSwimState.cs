using System;

namespace Shooter.Core
{
    public readonly struct CharacterSwimState : IEquatable<CharacterSwimState>
    {
        public CharacterSwimState(bool isSwimming, float surfaceY, bool isUnderwater)
        {
            IsSwimming = isSwimming;

            // 泳いでいない間の水面を残すと、同じく泳いでいない予測とサーバーの状態が Equals で食い違う。
            SurfaceY = isSwimming ? surfaceY : 0f;
            IsUnderwater = isSwimming && isUnderwater;
        }

        public static CharacterSwimState None => default;

        public bool IsSwimming { get; }

        public float SurfaceY { get; }

        public bool IsUnderwater { get; }

        public bool IsValid => !float.IsNaN(SurfaceY) && !float.IsInfinity(SurfaceY);

        public bool Equals(CharacterSwimState other)
        {
            return IsSwimming == other.IsSwimming && SurfaceY.Equals(other.SurfaceY) && IsUnderwater == other.IsUnderwater;
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterSwimState other && Equals(other);
        }

        public static bool operator ==(CharacterSwimState left, CharacterSwimState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterSwimState left, CharacterSwimState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var flags = (IsSwimming ? 1 : 0) | (IsUnderwater ? 2 : 0);
                return (flags * 397) ^ SurfaceY.GetHashCode();
            }
        }
    }
}

using System;

namespace Shooter.Core
{
    public readonly struct CharacterSwimState : IEquatable<CharacterSwimState>
    {
        public CharacterSwimState(bool isSwimming, float surfaceY, bool isUnderwater)
            : this(isSwimming, surfaceY, isUnderwater, 0f, 0)
        {
        }

        public CharacterSwimState(bool isSwimming, float surfaceY, bool isUnderwater, float breathUsedSeconds, int drownings)
        {
            IsSwimming = isSwimming;

            // 泳いでいない間の水面を残すと、同じく泳いでいない予測とサーバーの状態が Equals で食い違う。
            SurfaceY = isSwimming ? surfaceY : 0f;
            IsUnderwater = isSwimming && isUnderwater;
            BreathUsedSeconds = IsUnderwater ? breathUsedSeconds : 0f;
            Drownings = isSwimming ? drownings : 0;
        }

        public static CharacterSwimState None => default;

        public bool IsSwimming { get; }

        public float SurfaceY { get; }

        public bool IsUnderwater { get; }

        public float BreathUsedSeconds { get; }

        // 1 tick 分だけ持つ形にせず積むのは、差を取る側 (DrowningsSince) が tick を飛ばしても出来事を落とさないため。
        public int Drownings { get; }

        public bool IsValid => !float.IsNaN(SurfaceY) && !float.IsInfinity(SurfaceY)
            && BreathUsedSeconds >= 0f && !float.IsInfinity(BreathUsedSeconds) && Drownings >= 0;

        public int DrowningsSince(CharacterSwimState previous)
        {
            if (!IsSwimming)
            {
                return 0;
            }

            // 水から出て入り直すと数え直すので、前が泳いでいなければ今の数がすべて新しい
            return previous.IsSwimming ? Math.Max(0, Drownings - previous.Drownings) : Drownings;
        }

        public bool Equals(CharacterSwimState other)
        {
            return IsSwimming == other.IsSwimming && SurfaceY.Equals(other.SurfaceY) && IsUnderwater == other.IsUnderwater
                && BreathUsedSeconds.Equals(other.BreathUsedSeconds) && Drownings == other.Drownings;
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
                return (flags * 397) ^ SurfaceY.GetHashCode() ^ (BreathUsedSeconds.GetHashCode() * 31) ^ (Drownings << 2);
            }
        }
    }
}

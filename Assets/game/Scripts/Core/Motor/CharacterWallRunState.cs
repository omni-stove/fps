using System;
using System.Numerics;

namespace Shooter.Core
{
    public enum WallRunSide : byte
    {
        None = 0,
        Left = 1,
        Right = 2,
    }

    public readonly struct CharacterWallRunState : IEquatable<CharacterWallRunState>
    {
        public CharacterWallRunState(WallRunSide side, float elapsed, Vector3 normal)
        {
            Side = side;

            // 走っていない間の秒数と法線を残すと、同じく走っていない予測とサーバーの状態が Equals で食い違う。
            Elapsed = side == WallRunSide.None ? 0f : elapsed;
            Normal = side == WallRunSide.None ? Vector3.Zero : normal;
        }

        public static CharacterWallRunState None => default;

        public WallRunSide Side { get; }

        public float Elapsed { get; }

        public Vector3 Normal { get; }

        public bool IsRunning => Side != WallRunSide.None;

        public bool IsValid => Side <= WallRunSide.Right
            && IsFinite(Elapsed) && Elapsed >= 0f
            && IsFinite(Normal.X) && IsFinite(Normal.Y) && IsFinite(Normal.Z)
            && (!IsRunning || Normal.LengthSquared() > 0f);

        public bool Equals(CharacterWallRunState other)
        {
            return Side == other.Side && Elapsed.Equals(other.Elapsed) && Normal.Equals(other.Normal);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterWallRunState other && Equals(other);
        }

        public static bool operator ==(CharacterWallRunState left, CharacterWallRunState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterWallRunState left, CharacterWallRunState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((int)Side * 397) ^ Elapsed.GetHashCode() ^ Normal.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

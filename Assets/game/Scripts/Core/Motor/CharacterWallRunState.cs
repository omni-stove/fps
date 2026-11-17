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
            : this(side, elapsed, normal, Vector3.Zero)
        {
        }

        // lastWall は、着地するまで同じ壁に付き直さないために、走っていない間だけ持つ。
        public CharacterWallRunState(WallRunSide side, float elapsed, Vector3 normal, Vector3 lastWall)
        {
            Side = side;

            // 走っていない間の秒数と法線を残すと、同じく走っていない予測とサーバーの状態が Equals で食い違う。
            Elapsed = side == WallRunSide.None ? 0f : elapsed;
            Normal = side == WallRunSide.None ? Vector3.Zero : normal;

            // 走っている間は今の壁が Normal にあり、終えるときに移すので、前の壁を残すと同じ状態が Equals で食い違う。
            LastWall = side == WallRunSide.None ? lastWall : Vector3.Zero;
        }

        public static CharacterWallRunState None => default;

        public WallRunSide Side { get; }

        public float Elapsed { get; }

        public Vector3 Normal { get; }

        public Vector3 LastWall { get; }

        public bool IsRunning => Side != WallRunSide.None;

        public bool HasLastWall => LastWall != Vector3.Zero;

        public bool IsValid => Side <= WallRunSide.Right
            && IsFinite(Elapsed) && Elapsed >= 0f
            && IsFinite(Normal.X) && IsFinite(Normal.Y) && IsFinite(Normal.Z)
            && IsFinite(LastWall.X) && IsFinite(LastWall.Y) && IsFinite(LastWall.Z)
            && (!IsRunning || Normal.LengthSquared() > 0f);

        public static CharacterWallRunState LeftWall(Vector3 wall)
        {
            return new CharacterWallRunState(WallRunSide.None, 0f, Vector3.Zero, wall);
        }

        public bool Equals(CharacterWallRunState other)
        {
            return Side == other.Side && Elapsed.Equals(other.Elapsed) && Normal.Equals(other.Normal) && LastWall.Equals(other.LastWall);
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
                return ((int)Side * 397) ^ Elapsed.GetHashCode() ^ Normal.GetHashCode() ^ (LastWall.GetHashCode() * 31);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

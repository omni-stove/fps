using System;
using System.Numerics;

namespace Shooter.Core
{
    // 回した箱を許さず軸にそろえるのは、予測とサーバーが毎 tick 引く判定を、回転の誤差なしに同じ結果にするため。
    public readonly struct WaterBox : IEquatable<WaterBox>
    {
        public WaterBox(Vector3 min, Vector3 max, float surfaceY)
        {
            RequireFinite(min, nameof(min));
            RequireFinite(max, nameof(max));
            if (min.X > max.X || min.Y > max.Y || min.Z > max.Z)
            {
                throw new ArgumentException($"min {min} must not exceed max {max} on any axis", nameof(min));
            }

            if (float.IsNaN(surfaceY) || surfaceY < min.Y || surfaceY > max.Y)
            {
                throw new ArgumentOutOfRangeException(nameof(surfaceY), surfaceY, $"surfaceY must be between the bottom {min.Y} and the top {max.Y}");
            }

            Min = min;
            Max = max;
            SurfaceY = surfaceY;
        }

        public Vector3 Min { get; }

        public Vector3 Max { get; }

        public float SurfaceY { get; }

        public bool Contains(Vector3 position)
        {
            return position.X >= Min.X && position.X <= Max.X
                && position.Y >= Min.Y && position.Y <= Max.Y
                && position.Z >= Min.Z && position.Z <= Max.Z;
        }

        public bool Equals(WaterBox other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max) && SurfaceY.Equals(other.SurfaceY);
        }

        public override bool Equals(object obj)
        {
            return obj is WaterBox other && Equals(other);
        }

        public static bool operator ==(WaterBox left, WaterBox right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(WaterBox left, WaterBox right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (((Min.GetHashCode() * 397) ^ Max.GetHashCode()) * 397) ^ SurfaceY.GetHashCode();
            }
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!IsFinite(value.X) || !IsFinite(value.Y) || !IsFinite(value.Z))
            {
                throw new ArgumentException($"{name} {value} must be finite", name);
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

using System;

namespace Fps.Core.Navigation
{
    public readonly struct NavTriangle : IEquatable<NavTriangle>
    {
        public NavTriangle(int a, int b, int c, int area)
        {
            if (a < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(a), a, "vertex index must not be negative");
            }

            if (b < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(b), b, "vertex index must not be negative");
            }

            if (c < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(c), c, "vertex index must not be negative");
            }

            if (area < 0 || area >= NavMeshFile.AreaCount)
            {
                throw new ArgumentOutOfRangeException(nameof(area), area, "area must be 0 to 31");
            }

            A = a;
            B = b;
            C = c;
            Area = area;
        }

        public int A { get; }

        public int B { get; }

        public int C { get; }

        public int Area { get; }

        public bool Equals(NavTriangle other)
        {
            return A == other.A && B == other.B && C == other.C && Area == other.Area;
        }

        public static bool operator ==(NavTriangle left, NavTriangle right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NavTriangle left, NavTriangle right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is NavTriangle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (((A * 397) ^ B) * 397 ^ C) * 397 ^ Area;
        }
    }
}

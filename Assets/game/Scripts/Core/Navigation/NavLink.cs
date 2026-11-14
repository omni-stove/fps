using System;
using System.Numerics;

namespace Fps.Core.Navigation
{
    public readonly struct NavLink : IEquatable<NavLink>
    {
        public NavLink(Vector3 start, Vector3 end, bool bidirectional, int area, NavLinkKind kind)
        {
            if (!NavMeshFile.IsFinite(start))
            {
                throw new ArgumentException("link start must be finite", nameof(start));
            }

            if (!NavMeshFile.IsFinite(end))
            {
                throw new ArgumentException("link end must be finite", nameof(end));
            }

            if (area < 0 || area >= NavMeshFile.AreaCount)
            {
                throw new ArgumentOutOfRangeException(nameof(area), area, "area must be 0 to 31");
            }

            if (!Enum.IsDefined(typeof(NavLinkKind), kind))
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "unknown link kind");
            }

            Start = start;
            End = end;
            Bidirectional = bidirectional;
            Area = area;
            Kind = kind;
        }

        public Vector3 Start { get; }

        public Vector3 End { get; }

        public bool Bidirectional { get; }

        public int Area { get; }

        public NavLinkKind Kind { get; }

        public bool Equals(NavLink other)
        {
            return Start == other.Start && End == other.End && Bidirectional == other.Bidirectional && Area == other.Area && Kind == other.Kind;
        }

        public static bool operator ==(NavLink left, NavLink right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(NavLink left, NavLink right)
        {
            return !left.Equals(right);
        }

        public override bool Equals(object obj)
        {
            return obj is NavLink other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() * 397 ^ End.GetHashCode() ^ (Area << 2) ^ ((int)Kind << 8) ^ (Bidirectional ? 1 : 0);
        }
    }
}

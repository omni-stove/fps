using System;
using System.Numerics;

namespace Fps.Core
{
    public readonly struct CharacterCoverState : IEquatable<CharacterCoverState>
    {
        public CharacterCoverState(CoverSnapshot snapshot, float topY, CoverEdge edges, float appliedPeek)
        {
            Snapshot = snapshot;
            TopY = topY;
            Edges = edges;
            AppliedPeek = appliedPeek;
        }

        public static CharacterCoverState Out => default;

        public CoverSnapshot Snapshot { get; }

        public float TopY { get; }

        public CoverEdge Edges { get; }

        public float AppliedPeek { get; }

        public CoverPhase Phase => Snapshot.Phase;

        public CoverHeight Height => Snapshot.Height;

        public Vector3 Normal => Snapshot.Normal;

        public bool IsInCover => Snapshot.IsInCover;

        public bool IsPeeking => Snapshot.IsPeeking;

        public bool IsValid => Snapshot.IsValid
            && (int)Edges >= 0
            && Edges <= (CoverEdge.Left | CoverEdge.Right)
            && IsFinite(TopY)
            && IsFinite(AppliedPeek);

        public bool Equals(CharacterCoverState other)
        {
            return Snapshot.Equals(other.Snapshot)
                && TopY.Equals(other.TopY)
                && Edges == other.Edges
                && AppliedPeek.Equals(other.AppliedPeek);
        }

        public override bool Equals(object obj)
        {
            return obj is CharacterCoverState other && Equals(other);
        }

        public static bool operator ==(CharacterCoverState left, CharacterCoverState right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CharacterCoverState left, CharacterCoverState right)
        {
            return !left.Equals(right);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Snapshot.GetHashCode() * 397) ^ TopY.GetHashCode() ^ ((int)Edges << 4) ^ AppliedPeek.GetHashCode();
            }
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

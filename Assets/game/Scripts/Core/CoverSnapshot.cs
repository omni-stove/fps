using System;
using System.Numerics;

namespace Shooter.Core
{
    // ネット対戦の予測で過去の tick へ巻き戻して同じ入力を再シミュレーションするために、タイマーと覗く側の好みまで含める。
    public readonly struct CoverSnapshot : IEquatable<CoverSnapshot>
    {
        public CoverSnapshot(
            CoverPhase phase,
            CoverHeight height,
            Vector3 normal,
            float peekOffset,
            float enterElapsed,
            float backElapsed,
            float preferredSide)
        {
            Phase = phase;
            Height = height;
            Normal = normal;
            PeekOffset = peekOffset;
            EnterElapsed = enterElapsed;
            BackElapsed = backElapsed;
            PreferredSide = preferredSide;
        }

        public static CoverSnapshot Out => default;

        public CoverPhase Phase { get; }

        public CoverHeight Height { get; }

        public Vector3 Normal { get; }

        public float PeekOffset { get; }

        public float EnterElapsed { get; }

        public float BackElapsed { get; }

        public float PreferredSide { get; }

        public bool IsInCover => Phase != CoverPhase.Out;

        public bool IsPeeking => Phase == CoverPhase.Peeking;

        public bool IsValid
        {
            get
            {
                if (!Enum.IsDefined(typeof(CoverPhase), Phase) || !Enum.IsDefined(typeof(CoverHeight), Height))
                {
                    return false;
                }

                if (!IsFinite(Normal.X) || !IsFinite(Normal.Y) || !IsFinite(Normal.Z)
                    || !IsFinite(PeekOffset) || !IsFinite(PreferredSide)
                    || !IsFinite(EnterElapsed) || !IsFinite(BackElapsed)
                    || EnterElapsed < 0f || BackElapsed < 0f)
                {
                    return false;
                }

                return !IsInCover || CoverProbe.Horizontal(Normal) != Vector3.Zero;
            }
        }

        public bool Equals(CoverSnapshot other)
        {
            return Phase == other.Phase
                && Height == other.Height
                && Normal.Equals(other.Normal)
                && PeekOffset.Equals(other.PeekOffset)
                && EnterElapsed.Equals(other.EnterElapsed)
                && BackElapsed.Equals(other.BackElapsed)
                && PreferredSide.Equals(other.PreferredSide);
        }

        public override bool Equals(object obj)
        {
            return obj is CoverSnapshot other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)Phase, (int)Height, Normal, PeekOffset, EnterElapsed, BackElapsed, PreferredSide);
        }

        public static bool operator ==(CoverSnapshot left, CoverSnapshot right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CoverSnapshot left, CoverSnapshot right)
        {
            return !left.Equals(right);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}

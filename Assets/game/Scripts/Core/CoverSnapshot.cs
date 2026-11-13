using System;
using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// <see cref="CoverState"/> の中身 1 回分 (<see cref="CoverState.Capture"/> で写し、<see cref="CoverState.Restore"/> で戻す)。
    /// ネット対戦の予測で過去の tick へ巻き戻して同じ入力を再シミュレーションするために、タイマーと覗く側の好みまで含める。
    /// <c>default</c> はカバーの外で、覗く側の好みは右。
    /// </summary>
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

        /// <summary>カバーの外。</summary>
        public static CoverSnapshot Out => default;

        public CoverPhase Phase { get; }

        public CoverHeight Height { get; }

        /// <summary>張り付いている壁の、水平で長さ 1 のこちら向きの法線。</summary>
        public Vector3 Normal { get; }

        /// <summary><see cref="CoverState.PeekOffset"/>。</summary>
        public float PeekOffset { get; }

        /// <summary>入りかけになってからの秒数。</summary>
        public float EnterElapsed { get; }

        /// <summary>後ろの入力を続けている秒数。</summary>
        public float BackElapsed { get; }

        /// <summary>両側とも端のときに覗く側。負なら左、それ以外 (0 を含む) は右。</summary>
        public float PreferredSide { get; }

        public bool IsInCover => Phase != CoverPhase.Out;

        public bool IsPeeking => Phase == CoverPhase.Peeking;

        /// <summary>
        /// <see cref="CoverState.Restore"/> に渡せる値か。列挙が定義済みで、数値がすべて有限で、秒数が負でなく、
        /// カバー中なら法線に水平の成分がある。ネットワークから受け取った値の検査に使う。
        /// </summary>
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

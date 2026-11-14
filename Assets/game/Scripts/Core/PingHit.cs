using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="PingAim.Resolve"/> の結果。照準の先の何に印を付けるか。</summary>
    public readonly struct PingHit
    {
        private PingHit(bool hasTarget, PingKind kind, Vector3 position, int candidateIndex)
        {
            HasTarget = hasTarget;
            Kind = kind;
            Position = position;
            CandidateIndex = candidateIndex;
        }

        /// <summary>何も無い (照準の先が届く距離に無い)。</summary>
        public static PingHit None => new PingHit(false, PingKind.Location, Vector3.Zero, -1);

        /// <summary>付けるものがあるか。false なら <see cref="Kind"/> と <see cref="Position"/> は意味を持たない。</summary>
        public bool HasTarget { get; }

        public PingKind Kind { get; }

        /// <summary>印を付ける位置。敵なら敵の位置、場所なら照準が当たった点。</summary>
        public Vector3 Position { get; }

        /// <summary>敵なら候補の添字。場所か何も無いなら -1。</summary>
        public int CandidateIndex { get; }

        public static PingHit Enemy(int candidateIndex, Vector3 position) => new PingHit(true, PingKind.Enemy, position, candidateIndex);

        public static PingHit Location(Vector3 position) => new PingHit(true, PingKind.Location, position, -1);
    }
}

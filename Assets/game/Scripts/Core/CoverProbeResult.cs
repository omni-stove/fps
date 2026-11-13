using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="CoverProbe.Evaluate"/> の結果。</summary>
    public readonly struct CoverProbeResult
    {
        public CoverProbeResult(bool isWall, bool canEnter, CoverHeight height, CoverEdge edges, Vector3 normal, float distance)
        {
            IsWall = isWall;
            CanEnter = canEnter;
            Height = height;
            Edges = edges;
            Normal = normal;
            Distance = distance;
        }

        /// <summary>カバーにできる壁が無い。</summary>
        public static CoverProbeResult None => default;

        /// <summary>カバーにできる壁が届く距離にある (向きは問わない)。カバー中に壁が残っているかはこれで見る。</summary>
        public bool IsWall { get; }

        /// <summary><see cref="IsWall"/> で、しかも向いている方向から <see cref="CoverProbe.MaxEnterAngleDegrees"/> 以内。</summary>
        public bool CanEnter { get; }

        /// <summary>低い壁か高い壁か。<see cref="IsWall"/> が false なら意味を持たない。</summary>
        public CoverHeight Height { get; }

        /// <summary>壁が途切れる側。</summary>
        public CoverEdge Edges { get; }

        /// <summary>水平にして長さ 1 にした壁の法線 (こちら向き)。<see cref="IsWall"/> が false なら 0。</summary>
        public Vector3 Normal { get; }

        /// <summary>体の中心から壁までの水平距離。<see cref="IsWall"/> が false なら 0。</summary>
        public float Distance { get; }

        public bool IsAtLeftEdge => (Edges & CoverEdge.Left) != 0;

        public bool IsAtRightEdge => (Edges & CoverEdge.Right) != 0;
    }
}

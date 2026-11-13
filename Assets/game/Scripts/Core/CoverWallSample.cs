using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 物理で調べた、カバーにするかもしれない壁の様子 1 回分 (<see cref="CoverProbe.Evaluate"/> に渡す)。
    /// 座標は Y が上 (Unity と同じ並び)、長さは m、左右は壁に向いたときの左右。
    /// </summary>
    public readonly struct CoverWallSample
    {
        public CoverWallSample(float distance, Vector3 normal, float topHeight, bool wallOnLeft, bool wallOnRight)
        {
            HasHit = true;
            Distance = distance;
            Normal = normal;
            TopHeight = topHeight;
            WallOnLeft = wallOnLeft;
            WallOnRight = wallOnRight;
        }

        /// <summary>前方に何も当たらなかった。</summary>
        public static CoverWallSample None => default;

        /// <summary>前方への Ray が何かに当たったか。false なら他の値は意味を持たない。</summary>
        public bool HasHit { get; }

        /// <summary>体の中心から当たった面までの水平距離。</summary>
        public float Distance { get; }

        /// <summary>当たった面の法線 (こちら向き)。</summary>
        public Vector3 Normal { get; }

        /// <summary>壁の上端の、足元からの高さ。</summary>
        public float TopHeight { get; }

        /// <summary>壁沿いに左へ <see cref="CoverProbe.EdgeProbeDistance"/> ずらしても壁があったか。</summary>
        public bool WallOnLeft { get; }

        /// <summary>壁沿いに右へ <see cref="CoverProbe.EdgeProbeDistance"/> ずらしても壁があったか。</summary>
        public bool WallOnRight { get; }
    }
}

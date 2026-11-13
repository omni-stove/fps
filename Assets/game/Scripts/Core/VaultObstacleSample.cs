using System.Numerics;

namespace Fps.Core
{
    /// <summary>
    /// 物理で調べた、乗り越えるかもしれない前方の障害物の様子 1 回分 (<see cref="VaultProbe.Evaluate"/> に渡す)。
    /// 座標は Y が上 (Unity と同じ並び)、長さは m、高さは足元から。
    /// </summary>
    public readonly struct VaultObstacleSample
    {
        /// <param name="distance">体の中心から障害物の手前の面までの水平距離。</param>
        /// <param name="normal">手前の面の法線 (こちら向き)。</param>
        /// <param name="topHeight">障害物の上端の、足元からの高さ。</param>
        /// <param name="depth">手前の面から向こうの面までの奥行き。測れる範囲より厚ければ <see cref="float.PositiveInfinity"/>。</param>
        public VaultObstacleSample(float distance, Vector3 normal, float topHeight, float depth)
        {
            HasHit = true;
            Distance = distance;
            Normal = normal;
            TopHeight = topHeight;
            Depth = depth;
        }

        /// <summary>前方に何も当たらなかった。</summary>
        public static VaultObstacleSample None => default;

        /// <summary>前方への Ray が何かに当たったか。false なら他の値は意味を持たない。</summary>
        public bool HasHit { get; }

        /// <summary>体の中心から手前の面までの水平距離。</summary>
        public float Distance { get; }

        /// <summary>手前の面の法線 (こちら向き)。</summary>
        public Vector3 Normal { get; }

        /// <summary>上端の、足元からの高さ。</summary>
        public float TopHeight { get; }

        /// <summary>手前の面から向こうの面までの奥行き。測れる範囲より厚ければ <see cref="float.PositiveInfinity"/>。</summary>
        public float Depth { get; }
    }
}

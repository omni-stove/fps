using System.Numerics;

namespace Fps.Core
{
    /// <summary><see cref="IWorldCollision"/> の Raycast・掃引が当たった 1 か所。</summary>
    public readonly struct WorldHit
    {
        public WorldHit(Vector3 point, Vector3 normal, float distance, int colliderId, int layer)
        {
            Point = point;
            Normal = normal;
            Distance = distance;
            ColliderId = colliderId;
            Layer = layer;
        }

        /// <summary>当たった点。掃引ではカプセルが触れた面の上の点。</summary>
        public Vector3 Point { get; }

        /// <summary>当たった面の法線 (長さ 1)。</summary>
        public Vector3 Normal { get; }

        /// <summary>始点 (掃引ではカプセルの初めの位置) から当たるまでに進んだ距離。</summary>
        public float Distance { get; }

        /// <summary>当たったものを実装の中で引く Id。何を指すかは実装が決める。</summary>
        public int ColliderId { get; }

        /// <summary>当たったもののレイヤーの番号 (0〜31)。</summary>
        public int Layer { get; }
    }
}

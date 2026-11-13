using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// 弾で被害を受ける建造物。<see cref="StructureDamage.Apply"/> が、当たったコライダーの親をたどって見つけたものに渡す。
    /// 1 つの建造物が複数のコライダー (部品) を持つときは、部品ごとに呼ばれる。
    /// </summary>
    public interface IStructureHittable
    {
        /// <summary>建造物の部品に被害を与える。</summary>
        /// <param name="collider">被害を受けた部品のコライダー。この建造物の子孫。</param>
        /// <param name="point">着弾点 (爆発なら爆発の中心)。</param>
        /// <param name="damage">与える被害。0 以上。</param>
        void ApplyStructureHit(Collider collider, Vector3 point, int damage);
    }
}

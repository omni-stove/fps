using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="HitboxRewinder"/> で巻き戻す対象 1 体。Root の子孫のコライダーを当たり判定 (Hitbox) として扱う。</summary>
    public readonly struct RewindTarget
    {
        public RewindTarget(int id, Transform root, bool hittable)
            : this(id, root, hittable, null)
        {
        }

        public RewindTarget(int id, Transform root, bool hittable, NetStance stance)
        {
            Id = id;
            Root = root;
            Hittable = hittable;
            Stance = stance;
        }

        /// <summary><see cref="Fps.Core.LagCompensator"/> に記録したときの id。</summary>
        public int Id { get; }

        /// <summary>動かす Transform。この子孫のコライダーが一緒に動く。</summary>
        public Transform Root { get; }

        /// <summary>
        /// 当ててよいか。false (倒れている・撃った本人など) なら動かさず、Raycast はこの対象のコライダーを素通りする。
        /// </summary>
        public bool Hittable { get; }

        /// <summary>
        /// 巻き戻した時点のしゃがみに合わせてカプセルの高さを変える姿勢。null なら高さは今のまま。
        /// </summary>
        public NetStance Stance { get; }
    }
}

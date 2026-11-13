using UnityEngine;

namespace Fps.Net
{
    /// <summary><see cref="HitboxRewinder"/> の Raycast の命中。</summary>
    public readonly struct RewindHit
    {
        /// <summary>巻き戻した対象ではないもの (壁や床) に当たったときの <see cref="TargetId"/>。</summary>
        public const int NoTarget = -1;

        public RewindHit(RaycastHit raycastHit, int targetId, Vector3 targetPosition)
            : this(raycastHit, targetId, targetPosition, false)
        {
        }

        public RewindHit(RaycastHit raycastHit, int targetId, Vector3 targetPosition, bool targetCrouched)
        {
            RaycastHit = raycastHit;
            TargetId = targetId;
            TargetPosition = targetPosition;
            TargetCrouched = targetCrouched;
        }

        /// <summary>最も近い命中。点と距離は巻き戻した位置で測ったもの。</summary>
        public RaycastHit RaycastHit { get; }

        /// <summary>当たった対象の id。対象以外に当たったら <see cref="NoTarget"/>。</summary>
        public int TargetId { get; }

        /// <summary>当たった対象を置いていた (巻き戻した) 位置。対象以外に当たったら Vector3.zero。</summary>
        public Vector3 TargetPosition { get; }

        /// <summary>
        /// 当たったときの対象の姿勢 (巻き戻した時点のしゃがみ。履歴が無ければ今の姿勢)。
        /// 対象以外に当たった・対象が姿勢 (<see cref="RewindTarget.Stance"/>) を持たないときは false。
        /// </summary>
        public bool TargetCrouched { get; }

        public bool HitTarget => TargetId != NoTarget;
    }
}

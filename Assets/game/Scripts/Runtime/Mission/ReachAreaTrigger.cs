using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーがトリガーの Collider に入ったら目標を進める。目標がまだ Locked の間に入っていても、
    /// 中にいるまま Active になれば進める (<see cref="ObjectiveZoneMode.Reach"/>)。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ReachAreaTrigger : ObjectiveAreaBase
    {
        protected override ObjectiveZoneMode Mode => ObjectiveZoneMode.Reach;
    }
}

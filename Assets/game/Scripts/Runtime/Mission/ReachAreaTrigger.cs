using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class ReachAreaTrigger : ObjectiveAreaBase
    {
        protected override ObjectiveZoneMode Mode => ObjectiveZoneMode.Reach;
    }
}

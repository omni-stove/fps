using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    // 相手を何で持つか (Unity の当たり・ネット対戦のスナップショット) は場所で違うので、集め方を外から渡す (ILockOnTargets と同じ)。
    public interface IAimAssistTargets
    {
        void Collect(Vector3 eye, float range, List<Vector3> into);
    }
}

using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // Id の意味がオフライン (CombatEvents.OfflineId) とオンライン (プレイヤー Id) で違うので、相手の集め方と名前の引き先を組にして差し替える。
    public interface INameplateTargets
    {
        IParticipants Participants { get; }

        // 手元のプレイヤー自身は入れない。
        void CopyTo(Vector3 eye, List<NameplateTarget> targets);
    }
}

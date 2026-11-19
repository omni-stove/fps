using System.Collections.Generic;
using UnityEngine;

namespace Shooter.Runtime
{
    // 型を飛行機の Bot の操縦部品の公開メンバーに合わせるのは、今の操縦部品をそのままこの口で扱えるようにするため。
    public interface IBotPilot
    {
        IVehicleRider Rider { get; set; }

        IReadOnlyList<Transform> Waypoints { get; }

        bool FollowWaypointHeight { get; set; }

        // 小文字なのは、MonoBehaviour.enabled をそのまま実装に充てるため。
        bool enabled { get; set; }

        void SetWaypoints(params Transform[] points);

        void Release();
    }
}

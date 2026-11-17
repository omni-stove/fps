using UnityEngine;

namespace Shooter.Runtime
{
    // 全キャラクターを毎発探さず登録したものだけ調べるのは、ルールが Off の (誰も登録しない) ときに弾ごとの手間を 0 にするため。
    public interface INearMissTarget
    {
        Transform Root { get; }

        float NearMissRadius { get; }

        bool TryGetNearMissCenter(out Vector3 center);

        void OnNearMiss(HitscanNearMiss nearMiss);
    }
}

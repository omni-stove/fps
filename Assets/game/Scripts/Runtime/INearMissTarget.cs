using UnityEngine;

namespace Fps.Runtime
{
    // 全キャラクターを毎発探さず登録したものだけ調べるのは、ルールが Off の (誰も登録しない) ときに弾ごとの手間を 0 にするため。
    public interface INearMissTarget
    {
        Transform Root { get; }

        // 0 以下を返せば調べない。
        float NearMissRadius { get; }

        // 倒れているなど、今は数えないなら false。
        bool TryGetNearMissCenter(out Vector3 center);

        void OnNearMiss(HitscanNearMiss nearMiss);
    }
}

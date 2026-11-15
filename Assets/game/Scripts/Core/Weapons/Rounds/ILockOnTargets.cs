using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fps.Core
{
    // 相手を何で持つか (Unity の当たり・ネット対戦のプレイヤー) は場所で違うので、集め方を外から渡す。
    public interface ILockOnTargets
    {
        void Collect(Vector3 eye, Vector3 forward, float range, List<LockOnCandidate> into);

        // 弾は撃った後も飛び続けるので、Id を引き直さずに同じ相手の今の位置を返し続ける関数を渡す。相手が消えたら null を返す。
        Func<Vector3?> Follow(int id);
    }
}

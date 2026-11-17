using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 技のアニメ・カメラ・音を各ゲームが差し替える口。位置合わせ・操作の停止・結果には関わらない。
    // 毎フレームの動きは実装側が自分の Update で行う (技の秒数は ContextMeleeMove から読める)。
    public interface IContextMeleePresenter
    {
        void OnStarted(GameObject attacker, Damageable target, ContextMeleeMove move);

        void OnResolved(GameObject attacker, Damageable target, ContextMeleeMove move, int damage, bool killed);

        void OnInterrupted(GameObject attacker, Damageable target, ContextMeleeMove move, ContextMeleeInterruption reason);
    }
}

using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 振りのアニメ・音・当たりの手応え (画面の揺れ・止め) を各ゲームが差し替える口。判定とダメージには関わらない。
    public interface IMeleePresenter
    {
        void OnSwingStarted(GameObject attacker, MeleeSpec spec);

        void OnHit(GameObject attacker, Damageable target, Vector3 point, Vector3 direction, int damage);

        void OnMissed(GameObject attacker);
    }
}

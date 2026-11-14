using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 振りのアニメ・音・当たりの手応え (画面の揺れ・止め) を各ゲームが差し替える口。判定とダメージには関わらない。
    // MeleeController と EnemyMelee は、同じ GameObject でこれを実装したコンポーネントを探して使う。
    public interface IMeleePresenter
    {
        void OnSwingStarted(GameObject attacker, MeleeSpec spec);

        void OnHit(GameObject attacker, Damageable target, Vector3 point, Vector3 direction, int damage);

        void OnMissed(GameObject attacker);
    }
}

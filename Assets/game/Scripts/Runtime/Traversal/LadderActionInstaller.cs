using Shooter.Core;
using UnityEngine;
using LadderRule = Shooter.Core.Ladder;

namespace Shooter.Runtime.Traversal
{
    // Off で、まだ付けていなければ何も付けないのは、はしごを使わないゲームで移動の横取りを増やさないため。
    public sealed class LadderActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、組み込みの回避と乗り越え (MoveActionInstaller) の後に置く。
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            var mover = player.GetComponent<PlayerController>();
            var movement = player.GetComponent<LadderMovement>();
            if (mover == null || (movement == null && rules.Actions.Ladder != LadderRule.On))
            {
                if (movement != null)
                {
                    movement.Rule = LadderRule.Off;
                }

                return;
            }

            movement = PlayerActions.GetOrAdd<LadderMovement>(player);
            movement.Bind(mover, player.GetComponentInChildren<WeaponController>(true), player.GetComponentInChildren<AimDownSights>(true));
            movement.Rule = rules.Actions.Ladder;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnLoad()
        {
            PlayerActions.Register(new LadderActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new LadderActionInstaller());
        }
#endif
    }
}

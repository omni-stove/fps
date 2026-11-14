using Fps.Core;
using UnityEngine;
using LadderRule = Fps.Core.Ladder;

namespace Fps.Runtime.Traversal
{
    /// <summary>
    /// プレイヤーの <see cref="PlayerController"/> に <see cref="LadderMovement"/> を付け、ルールの <see cref="ActionRules.Ladder"/> に従わせる。
    /// つかまっている間にしまわせる武器と覗き込みは、プレイヤーとその子から探す。
    /// Off で、まだ付けていなければ何も付けない (はしごを使わないゲームでは移動の横取りを増やさない)。
    /// </summary>
    public sealed class LadderActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まないので、組み込みの回避と乗り越え (<see cref="MoveActionInstaller"/>) の後に置く。</summary>
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

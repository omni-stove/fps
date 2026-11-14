using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="PlayerController"/> を持つプレイヤーに <see cref="ParachuteMovement"/> を付け、ルールの <see cref="ActionRules.Parachute"/> を入れる。
    /// 起動時に <see cref="PlayerActions.Register"/> で自分を登録する。
    /// </summary>
    public sealed class ParachuteActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。回避と乗り越え (<see cref="MoveActionInstaller.InstallOrder"/>) を決めた後に呼ぶ。</summary>
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // Off のルールでも付けておくのは、キャンペーンなどが後からルールを差し替えて Attach し直したときに On にできるようにするため。
        // PlayerController が無い物 (標的など移動しない物) には付けない。
        public void Install(GameObject player, GameRules rules)
        {
            var mover = player.GetComponent<PlayerController>();
            if (mover == null || !mover.IsInitialized)
            {
                return;
            }

            var parachute = PlayerActions.GetOrAdd<ParachuteMovement>(player);
            if (!parachute.IsInitialized)
            {
                parachute.Initialize(mover);
            }

            parachute.Configure(rules);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnLoad()
        {
            PlayerActions.Register(new ParachuteActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new ParachuteActionInstaller());
        }
#endif
    }
}

using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class ParachuteActionInstaller : IPlayerActionInstaller
    {
        // 回避と乗り越え (MoveActionInstaller.InstallOrder) を決めた後に呼ぶため、それより後に置く。
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
            parachute.ApplyTuning(TraversalTuning.Current.Parachute);
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

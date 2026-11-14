using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class MeleeActionInstaller : IPlayerActionInstaller
    {
        // グレネード (400) の後。振れるかを決めるときに GrenadeThrower を読むが、付けるのは CharacterComponents なので順は問わない。
        public const int InstallOrder = 500;

        public int Order => InstallOrder;

        // 移動と射撃が無い物 (標的など) には振る体が無いので付けない。
        public void Install(GameObject player, GameRules rules)
        {
            if (player.GetComponent<PlayerController>() == null || player.GetComponentInChildren<WeaponController>(true) == null)
            {
                return;
            }

            PlayerActions.GetOrAdd<MeleeController>(player).Configure(rules);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAtStartup()
        {
            PlayerActions.Register(new MeleeActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new MeleeActionInstaller());
        }
#endif
    }
}

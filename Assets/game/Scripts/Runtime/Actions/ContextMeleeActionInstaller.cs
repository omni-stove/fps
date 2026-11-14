using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class ContextMeleeActionInstaller : IPlayerActionInstaller
    {
        // 近接 (MeleeActionInstaller) の後。技は MeleeController の入力と値から出すので、それが付いてから付ける。
        public const int InstallOrder = MeleeActionInstaller.InstallOrder + 10;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            var melee = player.GetComponent<MeleeController>();
            if (melee == null || !melee.IsInitialized)
            {
                return;
            }

            var action = PlayerActions.GetOrAdd<ContextMeleeAction>(player);
            action.Configure(rules);

            // 演出の差し替えが無いときだけ仮の演出を付ける。ゲームが付けた IContextMeleePresenter を上書きしない。
            if (action.Presenter == null && player.GetComponent<IContextMeleePresenter>() == null)
            {
                player.AddComponent<ContextMeleeCameraPresenter>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAtStartup()
        {
            PlayerActions.Register(new ContextMeleeActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new ContextMeleeActionInstaller());
        }
#endif
    }
}

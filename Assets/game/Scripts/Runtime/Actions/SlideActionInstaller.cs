using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class SlideActionInstaller : IPlayerActionInstaller
    {
        // CoverActionInstaller が付けたカバーを読むので、その後に呼ぶ。
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // 移動を持たない物 (標的など) は滑らないので付け足さない。
        public void Install(GameObject player, GameRules rules)
        {
            if (player.GetComponent<PlayerController>() == null)
            {
                return;
            }

            var slide = PlayerActions.GetOrAdd<SlideMovement>(player);
            var cover = player.GetComponent<CoverController>();
            slide.Blocker = () => cover != null && cover.IsInCover;
            slide.BindDamageable(player.GetComponent<Damageable>());
            slide.ApplyTuning(TraversalTuning.Current.Slide);
            slide.Configure(rules);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnLoad()
        {
            PlayerActions.Register(new SlideActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new SlideActionInstaller());
        }
#endif
    }
}

using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーに <see cref="SlideMovement"/> を (無ければ) 付け、<see cref="ActionRules.Slide"/> で有効・無効を決める。
    /// カバー中 (<see cref="CoverController.IsInCover"/>) は始めず、死亡・リスポーンでやめさせる。
    /// 組み込みの並び (<see cref="PlayerActions"/>) は変えず、読み込み時に <see cref="PlayerActions.Register"/> で自分を足す。
    /// </summary>
    public sealed class SlideActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。<see cref="CoverActionInstaller"/> が付けたカバーを読むので、その後に呼ぶ。</summary>
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

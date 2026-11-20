using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // GameSignals.Vision は画面に 1 つの値なので、敵やネットの体にも付ける CharacterComponents でなく、手元のプレイヤーにだけ呼ぶ PlayerActions.Attach から付ける。
    public sealed class VisionActionInstaller : IPlayerActionInstaller
    {
        // 組み込みの最初 (カバー) より前。PlayerActions.Attach の前に付けていた頃と同じく、ほかのアクションより先に付く。
        public const int InstallOrder = CoverActionInstaller.InstallOrder - 50;

        public int Order => InstallOrder;

        // 組み込みの並びに足さず自分で登録するのは、Shooter.Runtime が Shooter.Vision を参照できないため。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAtStartup()
        {
            PlayerActions.Register(new VisionActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new VisionActionInstaller());
        }
#endif

        // 見え方を切り替えるルールは無いので rules は読まない。キャンペーンが Attach し直しても GetOrAdd で増えない。
        public void Install(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            var vision = PlayerActions.GetOrAdd<VisionModeController>(player);

            // 無いときに null を入れないのは、後から Damageable を付ける組み立てでも VisionModeController が自分で探し直せるようにするため。
            var damageable = player.GetComponent<Damageable>();
            if (damageable != null)
            {
                vision.BindDamageable(damageable);
            }

            PlayerActions.GetOrAdd<ThermalHighlighter>(player).Controller = vision;
        }
    }
}

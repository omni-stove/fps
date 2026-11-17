using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class CoverActionInstaller : IPlayerActionInstaller
    {
        // カバー中かを読む取り付け (MoveActionInstaller) より先に呼ぶ。
        public const int InstallOrder = 100;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<CoverController>(player).Configure(rules);
        }
    }
}

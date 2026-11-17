using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class AbilityActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションを読まないので、組み込みの間の空いたところに置く。
        public const int InstallOrder = 375;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<AbilityHost>(player).Configure(rules);
        }
    }
}

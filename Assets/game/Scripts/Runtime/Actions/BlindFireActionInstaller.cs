using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class BlindFireActionInstaller : IPlayerActionInstaller
    {
        public const int InstallOrder = 200;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<BlindFireController>(player).Configure(rules);
        }
    }
}

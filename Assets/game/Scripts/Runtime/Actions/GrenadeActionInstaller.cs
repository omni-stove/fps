using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class GrenadeActionInstaller : IPlayerActionInstaller
    {
        public const int InstallOrder = 400;

        public int Order => InstallOrder;

        // 投げるのは CharacterComponents.Attach が付けた本体だけなので、ここでは付け足さない。
        public void Install(GameObject player, GameRules rules)
        {
            var thrower = player.GetComponent<GrenadeThrower>();
            if (thrower != null)
            {
                thrower.Configure(rules);
            }
        }
    }
}

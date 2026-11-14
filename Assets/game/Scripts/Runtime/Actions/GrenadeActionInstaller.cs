using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>プレイヤーに <see cref="GrenadeThrower"/> があれば、グレネードの有無と数をルールにする (<see cref="GrenadeThrower.Configure"/>)。</summary>
    public sealed class GrenadeActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。</summary>
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

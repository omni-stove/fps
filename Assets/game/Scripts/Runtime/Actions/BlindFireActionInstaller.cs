using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>プレイヤーに <see cref="BlindFireController"/> を (無ければ) 付け、<see cref="ActionRules.BlindFire"/> で有効・無効を決める。</summary>
    public sealed class BlindFireActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。</summary>
        public const int InstallOrder = 200;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<BlindFireController>(player).Configure(rules);
        }
    }
}

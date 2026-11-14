using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>プレイヤーに <see cref="CoverController"/> を (無ければ) 付け、<see cref="ActionRules.Cover"/> で有効・無効を決める。</summary>
    public sealed class CoverActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。カバー中かを読む取り付け (<see cref="MoveActionInstaller"/>) より先に呼ぶ。</summary>
        public const int InstallOrder = 100;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<CoverController>(player).Configure(rules);
        }
    }
}

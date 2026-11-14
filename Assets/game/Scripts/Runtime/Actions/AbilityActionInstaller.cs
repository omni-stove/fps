using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーに <see cref="AbilityHost"/> を (無ければ) 付け、<see cref="ActionRules.Abilities"/> で使えるかを決める。
    /// 枠に何を入れるかはロードアウトの持ち主 (オフラインは <see cref="GameBootstrap"/>) が決める。
    /// </summary>
    public sealed class AbilityActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションを読まないので、組み込みの間の空いたところに置く。</summary>
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<AbilityHost>(player).Configure(rules);
        }
    }
}

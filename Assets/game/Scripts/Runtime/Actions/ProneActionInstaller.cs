using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーの <see cref="PlayerController"/> の伏せ (<see cref="PlayerController.ProneRule"/>) をルールの <see cref="ActionRules.Prone"/> にする。
    /// Off にすると、伏せていても起き上がらせる (頭上が空いていれば次の Tick で)。
    /// </summary>
    public sealed class ProneActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まないので、並びは問わない。</summary>
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // PlayerController は伏せの入力と姿勢を持つので付け足さない。無ければ (標的など移動しない物) 伏せも無い。
        public void Install(GameObject player, GameRules rules)
        {
            var mover = player.GetComponent<PlayerController>();
            if (mover == null)
            {
                return;
            }

            mover.ProneRule = rules.Actions.Prone;
        }
    }
}

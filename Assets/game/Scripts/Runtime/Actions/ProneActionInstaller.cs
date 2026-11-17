using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class ProneActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、並びは問わない。
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

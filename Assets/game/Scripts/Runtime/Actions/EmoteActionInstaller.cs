using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class EmoteActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。
        public const int InstallOrder = 530;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            // Off で付いていなければ付けない。前に On で付けたものは Configure で止めて輪を下ろす。
            var controller = player.GetComponent<EmoteController>();
            if (controller == null && rules.Actions.Emote == Emote.Off)
            {
                return;
            }

            if (controller == null)
            {
                controller = player.AddComponent<EmoteController>();
            }

            controller.Configure(rules);
        }
    }
}

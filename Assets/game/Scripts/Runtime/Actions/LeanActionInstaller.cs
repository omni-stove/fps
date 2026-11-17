using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 傾けられるかは移動の Core (LeanStep) がカバー中などで決めるので、ルールでは切り替えない。
    public sealed class LeanActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。
        public const int InstallOrder = 520;

        public LeanActionInstaller()
            : this(HoldMode.Hold)
        {
        }

        public LeanActionInstaller(HoldMode mode)
        {
            if (mode != HoldMode.Hold && mode != HoldMode.Toggle)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown hold mode");
            }

            Mode = mode;
        }

        public HoldMode Mode { get; }

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            var controller = PlayerActions.GetOrAdd<LeanController>(player);
            controller.Mode = Mode;
        }
    }
}

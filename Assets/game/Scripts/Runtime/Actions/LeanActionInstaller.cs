using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// プレイヤーに <see cref="LeanController"/> を (無ければ) 付け、押している間か押すたびに切り替えるか (<see cref="Mode"/>) を設定する。
    /// 傾けられるかは移動の Core (<see cref="LeanStep"/>) がカバー中などで決めるので、ルールでは切り替えない。
    /// <see cref="PlayerActions"/> には自分では登録しない。プレイヤーを組み立てる側が <see cref="PlayerActions.Register"/> するか、直接 <see cref="Install"/> を呼ぶ。
    /// </summary>
    public sealed class LeanActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。</summary>
        public const int InstallOrder = 520;

        public LeanActionInstaller()
            : this(HoldMode.Hold)
        {
        }

        /// <param name="mode">付けた <see cref="LeanController"/> に設定するモード。</param>
        /// <exception cref="ArgumentOutOfRangeException">定義されていないモード。</exception>
        public LeanActionInstaller(HoldMode mode)
        {
            if (mode != HoldMode.Hold && mode != HoldMode.Toggle)
            {
                throw new ArgumentOutOfRangeException(nameof(mode), mode, "unknown hold mode");
            }

            Mode = mode;
        }

        /// <summary>付けた <see cref="LeanController"/> に設定する、押している間か押すたびに切り替えるか。</summary>
        public HoldMode Mode { get; }

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            var controller = PlayerActions.GetOrAdd<LeanController>(player);
            controller.Mode = Mode;
        }
    }
}

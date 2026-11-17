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

        // 組み込みの並びに足さず自分で登録するのは、IPlayerActionInstaller の決まりどおり既存のファイルを変えずに足すため。
        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlayerActions.Register(new LeanActionInstaller());
        }
    }
}

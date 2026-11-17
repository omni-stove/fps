using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class PingActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。
        public const int InstallOrder = 500;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<PingController>(player).Configure(rules);
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlayerActions.Register(new PingActionInstaller());
        }
    }
}

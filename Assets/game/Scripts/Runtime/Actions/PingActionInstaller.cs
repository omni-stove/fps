using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>プレイヤーに <see cref="PingController"/> を (無ければ) 付け、<see cref="ActionRules.Ping"/> で付けられるかを決める。</summary>
    public sealed class PingActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。</summary>
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

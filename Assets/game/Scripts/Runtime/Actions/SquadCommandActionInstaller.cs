using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーに <see cref="SquadCommandController"/> を (無ければ) 付けて設定する。
    /// 指示を受ける味方の bot がいなければ何も起きない (<c>[Squad] refused reason=NoRecipients</c>) ので、ルールでは切り替えない。
    /// </summary>
    public sealed class SquadCommandActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。</summary>
        public const int InstallOrder = 510;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<SquadCommandController>(player).Configure();
        }

        // Edit Mode (テストやシーン生成) では Play の初期化が走らないので、エディターの読み込みでも登録する。
#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Register()
        {
            PlayerActions.Register(new SquadCommandActionInstaller());
        }
    }
}

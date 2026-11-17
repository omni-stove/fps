using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    /// <summary>
    /// プレイヤーに <see cref="SquadCommandController"/> を (無ければ) 付けて設定する。
    /// 指示を受ける味方の bot がいなければ何も起きない (<c>[Squad] refused reason=NoRecipients</c>) ので、ルールでは切り替えない。
    /// </summary>
    public sealed class SquadCommandActionInstaller : IPlayerActionInstaller
    {
        /// <summary><see cref="Order"/>。ほかのアクションのコンポーネントは読まないので、組み込みの後に呼ぶ。</summary>
        public const int InstallOrder = 510;

        public const string TuningResourcePath = "Tuning/squad";

        public int Order => InstallOrder;

        public static SquadOrderSpec Tuning =>
            KitTuning.Load(c => c.SquadTuning, TuningResourcePath, SquadOrderJson.Parse, SquadOrderSpec.Default);

        // 実行時に付けるコンポーネントは Inspector で値を変えられないので、データの値を書き込んでから設定する。
        public void Install(GameObject player, GameRules rules)
        {
            var controller = PlayerActions.GetOrAdd<SquadCommandController>(player);
            controller.ApplySpec(Tuning);
            controller.Configure();
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

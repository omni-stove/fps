using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class StealthActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントを読まないので、どこに並んでもよい。組み込みの最後 (グレネード) より前に置く。
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // Off でも付けて Off を入れる。前に On で付けたものを Off に戻すのと、敵が Off を読んで今までどおりすぐ交戦するようにするため。
        public void Install(GameObject player, GameRules rules)
        {
            PlayerActions.GetOrAdd<StealthTarget>(player).Rule = rules.Actions.Stealth;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnPlay()
        {
            PlayerActions.Register(new StealthActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new StealthActionInstaller());
        }
#endif
    }
}

using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class StealthActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントを読まないので、どこに並んでもよい。組み込みの最後 (グレネード) より前に置く。
        public const int InstallOrder = 350;

        public const string TuningResourcePath = KitTuning.StealthResourcePath;

        public const string AwarenessIndicatorSlotName = "AwarenessIndicator";

        public static StealthNoiseSpec Noise =>
            KitTuning.Load(c => c.StealthTuning, TuningResourcePath, StealthNoiseJson.Parse, StealthNoiseSpec.Default);

        public int Order => InstallOrder;

        // Off でも付けて Off を入れる。前に On で付けたものを Off に戻すのと、敵が Off を読んで今までどおりすぐ交戦するようにするため。
        public void Install(GameObject player, GameRules rules)
        {
            var target = PlayerActions.GetOrAdd<StealthTarget>(player);
            target.Rule = rules.Actions.Stealth;
            target.ApplyNoise(Noise);
        }

        public static void RegisterContentSlots()
        {
            GameContentSlots.Register(
                AwarenessIndicatorSlotName,
                typeof(AwarenessIndicatorCatalog),
                c => c.AwarenessIndicator as AwarenessIndicatorCatalog,
                (c, v) => c.AwarenessIndicator = (AwarenessIndicatorCatalog)v);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnPlay()
        {
            PlayerActions.Register(new StealthActionInstaller());
            RegisterContentSlots();
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new StealthActionInstaller());
            RegisterContentSlots();
        }
#endif
    }
}

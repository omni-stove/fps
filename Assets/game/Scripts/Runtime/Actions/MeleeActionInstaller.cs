using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class MeleeActionInstaller : IPlayerActionInstaller
    {
        // グレネード (400) の後。振れるかを決めるときに GrenadeThrower を読むが、付けるのは CharacterComponents なので順は問わない。
        public const int InstallOrder = 500;

        public const string TuningResourcePath = "Tuning/melee";

        public int Order => InstallOrder;

        public static MeleeSpec Tuning => KitTuning.Load(c => c.MeleeTuning, TuningResourcePath, MeleeJson.Parse, MeleeSpec.Default);

        // 移動と射撃が無い物 (標的など) には振る体が無いので付けない。
        public void Install(GameObject player, GameRules rules)
        {
            if (player.GetComponent<PlayerController>() == null || player.GetComponentInChildren<WeaponController>(true) == null)
            {
                return;
            }

            // 実行時に付けるので Inspector の値は既定のままになる。ゲームの値は調整値の JSON から渡す。
            var melee = PlayerActions.GetOrAdd<MeleeController>(player);
            melee.ApplyTuning(Tuning);
            melee.Configure(rules);

            // 演出の差し替えが無いときだけ仮の演出を付ける。ゲームが付けた IMeleePresenter を上書きしない。
            if (melee.Presenter == null && player.GetComponent<IMeleePresenter>() == null)
            {
                player.AddComponent<ViewmodelMeleePresenter>();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterAtStartup()
        {
            PlayerActions.Register(new MeleeActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new MeleeActionInstaller());
        }
#endif
    }
}

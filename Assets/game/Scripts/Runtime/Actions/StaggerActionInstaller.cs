using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public sealed class StaggerActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントを読まないので、どこで呼んでもよい。移動の取り付け (MoveActionInstaller) の後に置く。
        public const int InstallOrder = 350;

        public const string TuningResourcePath = "Tuning/stagger";

        public int Order => InstallOrder;

        // ドメインの再読み込みを切った Play でも、前の Play のルールを持ち越さないよう Off に戻してから登録する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnLoad()
        {
            StaggerReaction.ActiveRule = Stagger.Off;
            StaggerReaction.ActiveSpec = null;
            PlayerActions.Register(new StaggerActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new StaggerActionInstaller());
        }
#endif

        public static StaggerTuning Tuning => KitTuning.Load(c => c.StaggerTuning, TuningResourcePath, StaggerJson.Parse, StaggerTuning.Default);

        // 敵には取り付けの口が無いので、ここでルールと調整値を渡し、そのとき居る敵に StaggerReaction を付ける。
        // 後から生む敵は、生む側 (射撃場の置く物など) が StaggerReaction を付けておけば同じルールと調整値で怯む。
        public void Install(GameObject player, GameRules rules)
        {
            var rule = rules.Actions.Stagger;
            var tuning = Tuning;
            StaggerReaction.ActiveRule = rule;
            StaggerReaction.ActiveSpec = tuning.Enemy;

            if (rule == Stagger.On)
            {
                foreach (var enemy in Object.FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
                {
                    PlayerActions.GetOrAdd<StaggerReaction>(enemy.gameObject);
                }
            }

            // シーンに置いた敵は取り付けより先に Inspector の値で結び付いているので、付いているものすべてに渡し直す。
            foreach (var reaction in Object.FindObjectsByType<StaggerReaction>(FindObjectsInactive.Include))
            {
                reaction.ApplySpec(tuning.Enemy);
            }

            // 標的など移動しない物には付けない。PlayerStagger は移動の横取りで歩きを遅くするので PlayerController が要る。
            if (player.GetComponent<PlayerController>() == null || player.GetComponent<Damageable>() == null)
            {
                return;
            }

            var stagger = PlayerActions.GetOrAdd<PlayerStagger>(player);
            stagger.ApplySpec(tuning.Player);
            stagger.Rule = rule;
        }
    }
}

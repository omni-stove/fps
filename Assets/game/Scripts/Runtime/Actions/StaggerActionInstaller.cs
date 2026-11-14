using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    public sealed class StaggerActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションのコンポーネントを読まないので、どこで呼んでもよい。移動の取り付け (MoveActionInstaller) の後に置く。
        public const int InstallOrder = 350;

        public int Order => InstallOrder;

        // ドメインの再読み込みを切った Play でも、前の Play のルールを持ち越さないよう Off に戻してから登録する。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void RegisterOnLoad()
        {
            StaggerReaction.ActiveRule = Stagger.Off;
            PlayerActions.Register(new StaggerActionInstaller());
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterInEditor()
        {
            PlayerActions.Register(new StaggerActionInstaller());
        }
#endif

        // 敵には取り付けの口が無いので、ここでルールを渡し、そのとき居る敵に StaggerReaction を付ける。
        // 後から生む敵は、生む側 (射撃場の置く物など) が StaggerReaction を付けておけば同じルールで怯む。
        public void Install(GameObject player, GameRules rules)
        {
            var rule = rules.Actions.Stagger;
            StaggerReaction.ActiveRule = rule;

            if (rule == Stagger.On)
            {
                foreach (var enemy in Object.FindObjectsByType<EnemyAgent>(FindObjectsInactive.Include))
                {
                    PlayerActions.GetOrAdd<StaggerReaction>(enemy.gameObject);
                }
            }

            // 標的など移動しない物には付けない。PlayerStagger は移動の横取りで歩きを遅くするので PlayerController が要る。
            if (player.GetComponent<PlayerController>() == null || player.GetComponent<Damageable>() == null)
            {
                return;
            }

            PlayerActions.GetOrAdd<PlayerStagger>(player).Rule = rule;
        }
    }
}

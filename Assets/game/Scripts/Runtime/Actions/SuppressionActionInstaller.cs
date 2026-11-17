using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 調整値をコンストラクターで受けるのは、ゲームが別の値で作ったものを PlayerActions.Register すれば、同じ型の組み込みと入れ替わるため。
    // 敵への取り付けもここで行うのは、オフラインでルールが決まる口がプレイヤーの取り付け (GameBootstrap・CampaignRunner) しか無いため。
    public sealed class SuppressionActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションの部品は読まないので、組み込みの間に置く (後ろに足された取り付けより先に終える)。
        public const int InstallOrder = 350;

        private readonly SuppressionSpec _spec;

        public SuppressionActionInstaller()
        {
        }

        public SuppressionActionInstaller(SuppressionSpec spec)
        {
            _spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        // 組み込みは PlayerActions の静的な並びで入口 (GameContent) が決まる前に作られるので、作るときではなく使うときに KitTuning から読む。
        public SuppressionSpec Spec => _spec ?? KitTuning.Suppression;

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            var rule = rules.Actions.Suppression;
            var spec = Spec;

            // Off では部品を増やさない。前に On で付けたものだけは Off に戻す。
            if (rule == Suppression.On || player.GetComponent<PlayerSuppression>() != null)
            {
                PlayerActions.GetOrAdd<PlayerSuppression>(player).Configure(rule, spec);
            }

            EnemySuppression.ConfigureAll(rule, spec);
        }
    }
}

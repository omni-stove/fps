using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    // 調整値をコンストラクターで受けるのは、ゲームが別の値で作ったものを PlayerActions.Register すれば、同じ型の組み込みと入れ替わるため。
    // 敵への取り付けもここで行うのは、オフラインでルールが決まる口がプレイヤーの取り付け (GameBootstrap・CampaignRunner) しか無いため。
    public sealed class SuppressionActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションの部品は読まないので、組み込みの間に置く (後ろに足された取り付けより先に終える)。
        public const int InstallOrder = 350;

        public SuppressionActionInstaller()
            : this(SuppressionSpec.Default)
        {
        }

        public SuppressionActionInstaller(SuppressionSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public SuppressionSpec Spec { get; }

        public int Order => InstallOrder;

        public void Install(GameObject player, GameRules rules)
        {
            var rule = rules.Actions.Suppression;

            // Off では部品を増やさない。前に On で付けたものだけは Off に戻す。
            if (rule == Suppression.On || player.GetComponent<PlayerSuppression>() != null)
            {
                PlayerActions.GetOrAdd<PlayerSuppression>(player).Configure(rule, Spec);
            }

            EnemySuppression.ConfigureAll(rule, Spec);
        }
    }
}

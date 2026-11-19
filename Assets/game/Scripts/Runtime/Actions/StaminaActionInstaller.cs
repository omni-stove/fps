using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // PlayerActions の組み込みの並びに入れず、読み込み時にも Register しないのは、使うゲームが自分で Register するまで今の動きを変えないため。
    // 調整値をコンストラクターで受けるのは、ゲームが別の値で作ったものを Register すれば、そのまま使えるようにするため。
    public sealed class StaminaActionInstaller : IPlayerActionInstaller
    {
        // ほかのアクションの部品は読まないので、組み込みの間に置く (後ろに足された取り付けより先に終える)。
        public const int InstallOrder = 350;

        public StaminaActionInstaller()
            : this(StaminaSpec.Default)
        {
        }

        public StaminaActionInstaller(StaminaSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public StaminaSpec Spec { get; }

        public int Order => InstallOrder;

        // 移動を持たない物 (標的など) は走らないので付け足さない。Off では部品を増やさず、前に On で付けたものだけ Off に戻す。
        public void Install(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (player.GetComponent<PlayerController>() == null)
            {
                return;
            }

            var rule = rules.Actions.Stamina;
            if (rule != Stamina.On && player.GetComponent<PlayerStamina>() == null)
            {
                return;
            }

            var stamina = PlayerActions.GetOrAdd<PlayerStamina>(player);
            stamina.BindDamageable(player.GetComponent<Damageable>());
            stamina.Configure(rule, Spec);
        }
    }
}

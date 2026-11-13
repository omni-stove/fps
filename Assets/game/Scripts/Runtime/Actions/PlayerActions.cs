using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーに TPS のアクションのコンポーネント (<see cref="CoverController"/>・<see cref="BlindFireController"/>) を付け、回避と乗り越えの有無も含めて
    /// ルールでどれを有効にするかを決める。オフラインの <see cref="GameBootstrap"/> と <see cref="CampaignRunner"/> がルールを読んだ後に呼ぶ。
    /// </summary>
    public static class PlayerActions
    {
        public const string LogTag = "[Actions]";

        /// <summary>
        /// player に <see cref="CoverController"/> と <see cref="BlindFireController"/> を (無ければ) 付け、rules で有効・無効を決める。
        /// player に <see cref="PlayerController"/> があれば回避 (<see cref="PlayerController.DodgeRule"/>) と
        /// 乗り越え (<see cref="PlayerController.VaultRule"/>) も rules にし、カバー中は回避させない。
        /// player に <see cref="GrenadeThrower"/> があれば、グレネードの有無と数も rules にする (<see cref="GrenadeThrower.Configure"/>)。
        /// 何度呼んでもコンポーネントは増えず、最後に渡した rules に従う。決めた内容を <see cref="FormatLog"/> の 1 行でログに出す。
        /// </summary>
        public static void Attach(GameObject player, GameRules rules)
        {
            if (player == null)
            {
                throw new ArgumentNullException(nameof(player));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            var cover = GetOrAdd<CoverController>(player);
            cover.Configure(rules);
            GetOrAdd<BlindFireController>(player).Configure(rules);
            ConfigureMover(player.GetComponent<PlayerController>(), cover, rules);

            // 投げるのは CharacterComponents.Attach が付けた本体だけなので、ここでは付け足さない。
            var thrower = player.GetComponent<GrenadeThrower>();
            if (thrower != null)
            {
                thrower.Configure(rules);
            }

            Debug.Log(FormatLog(rules.Actions), player);
        }

        /// <summary>
        /// アクションのルールのログ。例: <c>[Actions] cover=On blindFire=Off activeReload=Off downed=Off dodge=Off vault=Off</c>。
        /// </summary>
        public static string FormatLog(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            return $"{LogTag} cover={actions.Cover} blindFire={actions.BlindFire} activeReload={actions.ActiveReload} downed={actions.Downed}"
                + $" dodge={actions.Dodge} vault={actions.Vault}";
        }

        // PlayerController は回避と乗り越えの入力と動きを持つので付け足さない。無ければ (標的など移動しない物) どちらも無い。
        // 乗り越えはカバー中に止めなくても、カバーが Jump を消すので始まらない。
        private static void ConfigureMover(PlayerController mover, CoverController cover, GameRules rules)
        {
            if (mover == null)
            {
                return;
            }

            mover.DodgeRule = rules.Actions.Dodge;
            mover.DodgeBlocker = () => cover != null && cover.IsInCover;
            mover.VaultRule = rules.Actions.Vault;
        }

        private static T GetOrAdd<T>(GameObject player)
            where T : Component
        {
            var component = player.GetComponent<T>();
            return component != null ? component : player.AddComponent<T>();
        }
    }
}

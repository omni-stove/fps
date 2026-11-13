using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// プレイヤーに TPS のアクションのコンポーネント (<see cref="CoverController"/>・<see cref="BlindFireController"/>) を付け、
    /// ルールでどれを有効にするかを決める。オフラインの <see cref="GameBootstrap"/> と <see cref="CampaignRunner"/> がルールを読んだ後に呼ぶ。
    /// </summary>
    public static class PlayerActions
    {
        public const string LogTag = "[Actions]";

        /// <summary>
        /// player に <see cref="CoverController"/> と <see cref="BlindFireController"/> を (無ければ) 付け、rules で有効・無効を決める。
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

            GetOrAdd<CoverController>(player).Configure(rules);
            GetOrAdd<BlindFireController>(player).Configure(rules);
            Debug.Log(FormatLog(rules.Actions), player);
        }

        /// <summary>アクションのルールのログ。例: <c>[Actions] cover=On blindFire=Off activeReload=Off downed=Off</c>。</summary>
        public static string FormatLog(ActionRules actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            return $"{LogTag} cover={actions.Cover} blindFire={actions.BlindFire} activeReload={actions.ActiveReload} downed={actions.Downed}";
        }

        private static T GetOrAdd<T>(GameObject player)
            where T : Component
        {
            var component = player.GetComponent<T>();
            return component != null ? component : player.AddComponent<T>();
        }
    }
}

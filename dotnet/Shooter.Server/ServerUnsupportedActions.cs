using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    // サーバーが読まない actions の項目の一覧はここ 1 か所に持つ。サーバーが項目を扱うようになったら、ここから外す。
    // 名前はルールの JSON の鍵 (GameRulesJson) で返すのは、ログを見た人がそのままルールのファイルで探せるようにするため
    internal static class ServerUnsupportedActions
    {
        private static readonly Item[] _items =
        {
            new Item(GameRulesJson.StealthItem, a => a.Stealth != Stealth.Off),
        };

        public static IReadOnlyList<string> EnabledIn(GameRules rules)
        {
            ArgumentNullException.ThrowIfNull(rules);

            var actions = rules.Actions;
            var enabled = new List<string>();
            foreach (var item in _items)
            {
                if (item.IsOn(actions))
                {
                    enabled.Add(item.Key);
                }
            }

            return enabled;
        }

        private readonly struct Item
        {
            public Item(string key, Func<ActionRules, bool> isOn)
            {
                Key = key;
                IsOn = isOn;
            }

            public string Key { get; }

            public Func<ActionRules, bool> IsOn { get; }
        }
    }
}

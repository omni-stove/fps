using System;
using System.Globalization;
using System.Text;

namespace Shooter.Core
{
    // 書式を 1 か所に置くのは、取り込みとテストがログの行を同じ関数で作って突き合わせるため。
    public static class GameRulesLog
    {
        public const string LogTag = "[GameRules]";

        public static string Read(string source, GameRules rules)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            return LogTag
                + " read '" + (string.IsNullOrEmpty(source) ? "-" : source) + "'"
                + " emote=" + rules.Actions.Emote
                + " stamina=" + rules.Actions.Stamina
                + " respawnAnchor=" + Anchors(rules)
                + " idleWarningSeconds=" + rules.IdleWarningSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Anchors(GameRules rules)
        {
            var text = new StringBuilder();
            foreach (var anchor in rules.RespawnAnchors)
            {
                if (text.Length > 0)
                {
                    text.Append(',');
                }

                text.Append(anchor.ToString());
            }

            return text.ToString();
        }
    }
}

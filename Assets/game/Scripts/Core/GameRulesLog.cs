using System;
using System.Collections.Generic;
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
                + " respawnAnchor=" + Join(rules.RespawnAnchors)
                + " reloadInterruptedBy=" + Join(rules.Actions.ReloadInterruptedBy)
                + " aimWhileReloading=" + rules.Actions.AimWhileReloading
                + " idleWarningSeconds=" + rules.IdleWarningSeconds.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // 空の一覧を "-" にするのは、空のときも項目が消えずにログを同じ形で読めるようにするため。
        private static string Join<T>(IReadOnlyList<T> values)
        {
            var text = new StringBuilder();
            foreach (var value in values)
            {
                if (text.Length > 0)
                {
                    text.Append(',');
                }

                text.Append(value.ToString());
            }

            return text.Length > 0 ? text.ToString() : "-";
        }
    }
}

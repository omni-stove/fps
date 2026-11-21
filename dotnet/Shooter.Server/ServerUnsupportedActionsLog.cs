using System;
using System.Collections.Generic;

namespace Shooter.Server
{
    internal static class ServerUnsupportedActionsLog
    {
        public const string Tag = "[Rules]";

        // 項目が無いときに null を返すのは、全部オフのルールで何も書かせないため
        public static string Warning(string rulesName, IReadOnlyList<string> actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            if (actions.Count == 0)
            {
                return null;
            }

            return Tag + " warning: actions " + string.Join(", ", actions) + " are on in rules " + rulesName
                + " but the server does not support them; they are ignored";
        }
    }
}

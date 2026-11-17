using System.Globalization;

namespace Shooter.Core
{
    // MatchLog と同じく key=value を空白で区切り、headless のサーバーのログからダウン・蘇生・出血で倒れたことを拾えるようにする。
    public static class NetDownedLog
    {
        public const string Tag = "[Net]";

        public const string NoKiller = "none";

        public static string Settings(ActionRules actions, float range)
        {
            if (actions == null)
            {
                throw new System.ArgumentNullException(nameof(actions));
            }

            return Tag + " downed rules downed=" + actions.Downed
                + " bleedout=" + actions.DownedBleedoutSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s"
                + " health=" + actions.DownedHealthFraction.ToString("F2", CultureInfo.InvariantCulture)
                + " revive=" + actions.ReviveSeconds.ToString("F2", CultureInfo.InvariantCulture) + "s"
                + " range=" + range.ToString("F2", CultureInfo.InvariantCulture) + "m";
        }

        public static string Downed(int attackerId, int victimId)
        {
            return Tag + " downed attacker=" + Id(attackerId) + " victim=" + Id(victimId);
        }

        public static string Revived(int reviverId, int targetId)
        {
            return Tag + " revived reviver=" + Id(reviverId) + " target=" + Id(targetId);
        }

        public static string Bleedout(int playerId, int? killerId)
        {
            return Tag + " bleedout player=" + Id(playerId) + " killer=" + (killerId.HasValue ? Id(killerId.Value) : NoKiller);
        }

        public static string Rejected(int reviverId, int targetId, ReviveRejection reason)
        {
            return Tag + " rejected revive reviver=" + Id(reviverId) + " target=" + Id(targetId) + " reason=" + reason;
        }

        private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}

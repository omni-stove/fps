using System.Globalization;

namespace Fps.Core
{
    /// <summary>
    /// ネット対戦のダウンと蘇生の <c>[Net]</c> 付きログの文言。<see cref="MatchLog"/> と同じく key=value を空白で区切り、
    /// headless のサーバーのログからダウン・蘇生・出血で倒れたことを拾えるようにする。
    /// </summary>
    public static class NetDownedLog
    {
        public const string Tag = "[Net]";

        /// <summary>倒した相手がいない (出血で倒れた人が自分にしか倒されていない) ときに killer= に書く値。</summary>
        public const string NoKiller = "none";

        /// <summary>
        /// 試合の開始時に、ダウンと蘇生のルール。秒・割合は小数 2 桁、range は起こせる距離 (m)。
        /// </summary>
        /// <example><c>[Net] downed rules downed=Revive bleedout=15.00s health=0.30 revive=3.00s range=1.50m</c></example>
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

        /// <example><c>[Net] downed attacker=2 victim=1</c></example>
        public static string Downed(int attackerId, int victimId)
        {
            return Tag + " downed attacker=" + Id(attackerId) + " victim=" + Id(victimId);
        }

        /// <example><c>[Net] revived reviver=0 target=1</c></example>
        public static string Revived(int reviverId, int targetId)
        {
            return Tag + " revived reviver=" + Id(reviverId) + " target=" + Id(targetId);
        }

        /// <summary>ダウン中に、とどめを刺されずに倒れた (出血の秒数が過ぎた・チームの全員が動けなくなった)。</summary>
        /// <example><c>[Net] bleedout player=1 killer=2</c>、倒した相手がいなければ <c>[Net] bleedout player=1 killer=none</c></example>
        public static string Bleedout(int playerId, int? killerId)
        {
            return Tag + " bleedout player=" + Id(playerId) + " killer=" + (killerId.HasValue ? Id(killerId.Value) : NoKiller);
        }

        /// <example><c>[Net] rejected revive reviver=0 target=1 reason=TooFar</c></example>
        public static string Rejected(int reviverId, int targetId, ReviveRejection reason)
        {
            return Tag + " rejected revive reviver=" + Id(reviverId) + " target=" + Id(targetId) + " reason=" + reason;
        }

        private static string Id(int id) => id.ToString(CultureInfo.InvariantCulture);
    }
}

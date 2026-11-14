using System;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fps.Core
{
    /// <summary>
    /// バトルロイヤル (<see cref="BattleRoyaleSession"/>) の <c>[Mode]</c> 付きログの文言。参加者の出入りや状態・キルは <see cref="ModeLog"/> のものを使い、
    /// ここにはダウン・蘇生・出血・全滅で倒れたこと・分隊の脱落と順位を置く。人は ID、分隊は陣営の ID の数字で書き、分からない人は <c>-</c>。
    /// </summary>
    public static class BattleRoyaleLog
    {
        public const string Tag = ModeLog.Tag;

        /// <example><c>[Mode] downed player=2 by=0</c>、ダウンさせた人が分からなければ <c>[Mode] downed player=2 by=-</c></example>
        public static string Downed(int playerId, int? downerId)
        {
            return Tag + " downed player=" + Number(playerId) + " by=" + Optional(downerId);
        }

        /// <example><c>[Mode] revived player=2 by=3</c></example>
        public static string Revived(int reviverId, int playerId)
        {
            return Tag + " revived player=" + Number(playerId) + " by=" + Number(reviverId);
        }

        /// <example><c>[Mode] bledout player=2 credit=0</c>、倒したことにする敵がいなければ <c>credit=-</c></example>
        public static string BledOut(int playerId, int? creditId)
        {
            return Tag + " bledout player=" + Number(playerId) + " credit=" + Optional(creditId);
        }

        /// <summary>分隊に立っている人がいなくなり、ダウン中の人を倒した。</summary>
        /// <example><c>[Mode] squad-wiped player=3 credit=0</c></example>
        public static string SquadWiped(int playerId, int? creditId)
        {
            return Tag + " squad-wiped player=" + Number(playerId) + " credit=" + Optional(creditId);
        }

        /// <summary>分隊が脱落した。順位と、まだ残っている分隊の数を書く。</summary>
        /// <example><c>[Mode] eliminated squad=2 place=3 remaining=2</c></example>
        public static string Eliminated(Side squad, int place, int remaining)
        {
            return Tag + " eliminated squad=" + Number(squad.Id) + " place=" + Number(place) + " remaining=" + Number(remaining);
        }

        /// <summary>試合が終わったときの全分隊の順位。順位の昇順 (同じなら分隊の ID の昇順) に並べる。</summary>
        /// <example><c>[Mode] placements winner=1 1:squad=1 2:squad=0 3:squad=2 3:squad=3</c>、引き分けなら <c>winner=draw</c></example>
        public static string Placements(BattleRoyaleSession session)
        {
            if (session == null)
            {
                throw new ArgumentNullException(nameof(session));
            }

            var winner = session.Mode.Winner;
            var text = new StringBuilder(Tag).Append(" placements winner=")
                .Append(winner.HasValue ? Number(winner.Value.Id) : ModeLog.Draw);
            foreach (var placement in session.GetPlacements())
            {
                text.Append(' ').Append(Number(placement.Place)).Append(":squad=").Append(Number(placement.Squad.Id));
            }

            return text.ToString();
        }

        /// <summary>bot の用事 (<see cref="BotErrand"/>) が変わった。用事が無ければ行き先は書かない。</summary>
        /// <example><c>[Mode] errand player=3 kind=Collect at=(4.00, 0.00, 12.00)</c>、用事が無ければ <c>[Mode] errand player=3 kind=None</c></example>
        public static string Errand(int playerId, BotErrandKind kind, Vector3 destination)
        {
            var text = Tag + " errand player=" + Number(playerId) + " kind=" + kind;
            if (kind == BotErrandKind.None)
            {
                return text;
            }

            return text + " at=(" + destination.X.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + destination.Y.ToString("F2", CultureInfo.InvariantCulture)
                + ", " + destination.Z.ToString("F2", CultureInfo.InvariantCulture) + ")";
        }

        private static string Optional(int? value)
        {
            return value.HasValue ? Number(value.Value) : "-";
        }

        private static string Number(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}

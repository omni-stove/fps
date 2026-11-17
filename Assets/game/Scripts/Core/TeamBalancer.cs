using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// 次の試合の前に、2 チームの人数の差を上限に収めるため誰を移すかを決める。差が上限を超えているときだけ、多いチームの
    /// 最近入った人から少ないチームへ移す。同じパーティの人はまとめて移すか、移さない。
    /// </summary>
    public static class TeamBalancer
    {
        /// <param name="alpha">Alpha にいる人。入った順 (古い人が先)。</param>
        /// <param name="bravo">Bravo にいる人。入った順 (古い人が先)。</param>
        /// <param name="maxDifference">人数の差の上限。0 なら誰も移さない。</param>
        /// <returns>移す人。移す順 (最近入った人が先)。</returns>
        /// <exception cref="ArgumentOutOfRangeException">maxDifference が負。</exception>
        /// <exception cref="ArgumentException">同じ playerId が 2 回以上ある。</exception>
        public static IReadOnlyList<TeamMove> Plan(IReadOnlyList<TeamBalanceMember> alpha, IReadOnlyList<TeamBalanceMember> bravo, int maxDifference)
        {
            if (alpha == null)
            {
                throw new ArgumentNullException(nameof(alpha));
            }

            if (bravo == null)
            {
                throw new ArgumentNullException(nameof(bravo));
            }

            if (maxDifference < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDifference), maxDifference, "maxDifference must not be negative");
            }

            RequireDistinct(alpha, bravo);
            var difference = alpha.Count - bravo.Count;
            if (maxDifference == 0 || Math.Abs(difference) <= maxDifference)
            {
                return Array.Empty<TeamMove>();
            }

            var from = difference > 0 ? Team.Alpha : Team.Bravo;
            var to = difference > 0 ? Team.Bravo : Team.Alpha;
            var units = UnitsNewestFirst(difference > 0 ? alpha : bravo);
            var moved = new bool[units.Count];
            var moves = new List<TeamMove>();
            var gap = Math.Abs(difference);
            while (gap > maxDifference)
            {
                var pick = Pick(units, moved, gap, maxDifference);
                if (pick < 0)
                {
                    break;
                }

                moved[pick] = true;
                foreach (var playerId in units[pick])
                {
                    moves.Add(new TeamMove(playerId, from, to));
                }

                gap -= 2 * units[pick].Count;
            }

            return moves;
        }

        // 最近入ったパーティを移して逆のチームが上限を超えるより、古くても上限に収まる組を移すほうがそろうので、収まる組を先に探す。
        // 収まる組が無ければ、差が縮まる組を移す
        private static int Pick(List<List<int>> units, bool[] moved, int gap, int maxDifference)
        {
            var fallback = -1;
            for (var i = 0; i < units.Count; i++)
            {
                var size = units[i].Count;
                if (moved[i] || size >= gap)
                {
                    continue;
                }

                if (gap - 2 * size >= -maxDifference)
                {
                    return i;
                }

                if (fallback < 0)
                {
                    fallback = i;
                }
            }

            return fallback;
        }

        // パーティの番を最初に入った人ではなく最後に入った人で決めるのは、最近そろったパーティを古い 1 人より先に移すため
        private static List<List<int>> UnitsNewestFirst(IReadOnlyList<TeamBalanceMember> members)
        {
            var units = new List<List<int>>();
            var parties = new Dictionary<string, List<int>>(StringComparer.Ordinal);
            for (var i = members.Count - 1; i >= 0; i--)
            {
                var member = members[i];
                if (member.PartyId == null)
                {
                    units.Add(new List<int> { member.PlayerId });
                    continue;
                }

                if (!parties.TryGetValue(member.PartyId, out var party))
                {
                    party = new List<int>();
                    parties.Add(member.PartyId, party);
                    units.Add(party);
                }

                party.Add(member.PlayerId);
            }

            return units;
        }

        private static void RequireDistinct(IReadOnlyList<TeamBalanceMember> alpha, IReadOnlyList<TeamBalanceMember> bravo)
        {
            var seen = new HashSet<int>();
            foreach (var members in new[] { alpha, bravo })
            {
                for (var i = 0; i < members.Count; i++)
                {
                    if (!seen.Add(members[i].PlayerId))
                    {
                        throw new ArgumentException("player " + members[i].PlayerId + " is listed more than once", nameof(alpha));
                    }
                }
            }
        }
    }
}

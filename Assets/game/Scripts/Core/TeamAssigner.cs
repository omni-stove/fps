using System;

namespace Shooter.Core
{
    /// <summary>
    /// 新しく入る人のチームを、今いる人数と点数から決める。人数の少ないチームに入れ、同じなら点の低いチーム、
    /// それも同じなら Alpha に入れる。誰も抜けなければ Alpha、Bravo、Alpha…と交互になる。
    /// 同じパーティの人がもういれば、そのチームが満員でない限りそこに入れる。
    /// </summary>
    public static class TeamAssigner
    {
        /// <param name="alphaPlayers">Alpha に今いる人数。切断して戻るのを待っている人は数えない。</param>
        /// <param name="bravoPlayers">Bravo に今いる人数。</param>
        /// <param name="alphaScore">Alpha の点数。</param>
        /// <param name="bravoScore">Bravo の点数。</param>
        /// <returns>入れるチーム。</returns>
        public static Team Choose(int alphaPlayers, int bravoPlayers, int alphaScore, int bravoScore)
        {
            if (alphaPlayers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaPlayers), alphaPlayers, "player count must not be negative");
            }

            if (bravoPlayers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoPlayers), bravoPlayers, "player count must not be negative");
            }

            if (alphaPlayers != bravoPlayers)
            {
                return alphaPlayers < bravoPlayers ? Team.Alpha : Team.Bravo;
            }

            if (alphaScore != bravoScore)
            {
                return alphaScore < bravoScore ? Team.Alpha : Team.Bravo;
            }

            return Team.Alpha;
        }

        /// <summary>
        /// パーティで入る人のチームを決める。同じパーティの人が多いほうのチーム (<see cref="PartyTeam"/>) に、
        /// そのチームの人数が <paramref name="teamCapacity"/> に届いていなければ入れる。同じパーティの人がいない・
        /// 両チームに同じ数ずついる・満員なら <see cref="Choose(int, int, int, int)"/> と同じく少ない方へ入れる。
        /// </summary>
        /// <param name="alphaPlayers">Alpha に今いる人数。少ない方を選ぶときに使う。</param>
        /// <param name="bravoPlayers">Bravo に今いる人数。</param>
        /// <param name="alphaScore">Alpha の点数。</param>
        /// <param name="bravoScore">Bravo の点数。</param>
        /// <param name="alphaPartyMembers">Alpha にいる、入る人と同じパーティの人数。</param>
        /// <param name="bravoPartyMembers">Bravo にいる、入る人と同じパーティの人数。</param>
        /// <param name="alphaSeated">満員かを見るときの Alpha の人数。</param>
        /// <param name="bravoSeated">満員かを見るときの Bravo の人数。</param>
        /// <param name="teamCapacity">1 チームに入れる人数 (<see cref="TeamCapacity"/>)。</param>
        /// <returns>入れるチーム。</returns>
        /// <remarks>
        /// 満員かを見る人数を <paramref name="alphaPlayers"/> と分けて受けるのは、サーバーの定員が Bot を数えないため。
        /// Bot で埋まったチームを満員と見ると、Bot がいるだけでパーティが分かれてしまう。
        /// </remarks>
        public static Team Choose(
            int alphaPlayers,
            int bravoPlayers,
            int alphaScore,
            int bravoScore,
            int alphaPartyMembers,
            int bravoPartyMembers,
            int alphaSeated,
            int bravoSeated,
            int teamCapacity)
        {
            if (alphaSeated < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaSeated), alphaSeated, "player count must not be negative");
            }

            if (bravoSeated < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoSeated), bravoSeated, "player count must not be negative");
            }

            if (teamCapacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(teamCapacity), teamCapacity, "teamCapacity must be at least 1");
            }

            var fallback = Choose(alphaPlayers, bravoPlayers, alphaScore, bravoScore);
            var partyTeam = PartyTeam(alphaPartyMembers, bravoPartyMembers);
            if (!partyTeam.HasValue)
            {
                return fallback;
            }

            var seated = partyTeam.Value == Team.Alpha ? alphaSeated : bravoSeated;
            return seated < teamCapacity ? partyTeam.Value : fallback;
        }

        /// <summary>同じパーティの人が多いほうのチーム。どちらにもいないか同じ数ずつなら null。</summary>
        public static Team? PartyTeam(int alphaPartyMembers, int bravoPartyMembers)
        {
            if (alphaPartyMembers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(alphaPartyMembers), alphaPartyMembers, "party member count must not be negative");
            }

            if (bravoPartyMembers < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(bravoPartyMembers), bravoPartyMembers, "party member count must not be negative");
            }

            if (alphaPartyMembers == bravoPartyMembers)
            {
                return null;
            }

            return alphaPartyMembers > bravoPartyMembers ? Team.Alpha : Team.Bravo;
        }

        /// <summary>試合に入れる人数を 2 チームで分けたときの 1 チームの人数。割り切れなければ多い方に合わせる。</summary>
        public static int TeamCapacity(int maxPlayers)
        {
            if (maxPlayers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPlayers), maxPlayers, "maxPlayers must be at least 1");
            }

            return maxPlayers / 2 + maxPlayers % 2;
        }
    }
}

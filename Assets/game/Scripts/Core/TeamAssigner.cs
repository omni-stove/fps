using System;

namespace Shooter.Core
{
    public static class TeamAssigner
    {
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

        // 満員かを見る人数 (alphaSeated / bravoSeated) を alphaPlayers と分けて受けるのは、サーバーの定員が Bot を数えないため。
        // Bot で埋まったチームを満員と見ると、Bot がいるだけでパーティが分かれてしまう。
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

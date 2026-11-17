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
    }
}

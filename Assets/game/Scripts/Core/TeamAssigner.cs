using System;

namespace Fps.Core
{
    /// <summary>
    /// 新しく入る人のチームを、今いる人数と点数から決める。人数の少ないチームに入れ、同じなら点の低いチーム、
    /// それも同じなら Alpha に入れる。誰も抜けなければ Alpha、Bravo、Alpha…と交互になる。
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
    }
}

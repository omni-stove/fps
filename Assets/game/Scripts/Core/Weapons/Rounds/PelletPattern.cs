using System;
using System.Numerics;

namespace Fps.Core
{
    public static class PelletPattern
    {
        public static int Count(RoundSpec round)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            return 1;
        }

        public static Vector3 Direction(RoundSpec round, Vector3 center, int playerId, int shotIndex, int pelletIndex)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            return center;
        }
    }
}

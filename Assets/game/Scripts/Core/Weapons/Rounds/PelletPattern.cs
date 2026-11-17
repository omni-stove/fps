using System;
using System.Numerics;

namespace Shooter.Core
{
    public static class PelletPattern
    {
        public static int Count(RoundSpec round)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            return round.PelletCount;
        }

        public static Vector3 Direction(RoundSpec round, Vector3 center, int playerId, int shotIndex, int pelletIndex)
        {
            if (round == null)
            {
                throw new ArgumentNullException(nameof(round));
            }

            if (pelletIndex < 0 || pelletIndex >= round.PelletCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(pelletIndex), pelletIndex, "pelletIndex must be between 0 and " + (round.PelletCount - 1));
            }

            // 0° でも正規化し直さないのは、1 本だけの武器の向きを散らばりを掛けた向きと 1 ビットも変えないため。
            if (round.PelletSpreadDegrees == 0f)
            {
                return center;
            }

            return SpreadPattern.Direction(
                center,
                round.PelletSpreadDegrees,
                playerId,
                shotIndex,
                pelletIndex,
                SpreadPattern.PelletPolarStream,
                SpreadPattern.PelletAzimuthStream);
        }
    }
}

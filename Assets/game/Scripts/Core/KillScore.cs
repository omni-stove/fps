using System;

namespace Shooter.Core
{
    public sealed class KillScore : IScoreRule
    {
        public double PointsPerKill { get; }

        public KillScore(double pointsPerKill = 1d)
        {
            if (!(pointsPerKill > 0d) || double.IsInfinity(pointsPerKill))
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerKill), pointsPerKill, "pointsPerKill must be positive and finite");
            }

            PointsPerKill = pointsPerKill;
        }

        public double PointsForKill(ModeSession session, int killerId, int victimId) => PointsPerKill;

        public double PointsPerSecond(ModeSession session, Side side) => 0d;

        public void Advance(ModeSession session, float seconds)
        {
        }
    }
}

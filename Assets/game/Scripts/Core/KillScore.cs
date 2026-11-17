using System;

namespace Shooter.Core
{
    public sealed class KillScore : IScoreRule
    {
        public double PointsPerKill { get; }

        public double PointsPerAssist { get; }

        public KillScore(double pointsPerKill = 1d, double pointsPerAssist = 0d)
        {
            if (!(pointsPerKill > 0d) || double.IsInfinity(pointsPerKill))
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerKill), pointsPerKill, "pointsPerKill must be positive and finite");
            }

            if (!(pointsPerAssist >= 0d) || double.IsInfinity(pointsPerAssist))
            {
                throw new ArgumentOutOfRangeException(nameof(pointsPerAssist), pointsPerAssist, "pointsPerAssist must not be negative and must be finite");
            }

            PointsPerKill = pointsPerKill;
            PointsPerAssist = pointsPerAssist;
        }

        public double PointsForKill(ModeSession session, int killerId, int victimId) => PointsPerKill;

        public double PointsForAssist(ModeSession session, int assisterId, int victimId) => PointsPerAssist;

        public double PointsPerSecond(ModeSession session, Side side) => 0d;

        public void Advance(ModeSession session, float seconds)
        {
        }
    }
}

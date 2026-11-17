using System;

namespace Shooter.Core
{
    /// <summary>
    /// 別の陣営を倒すたびに倒した人の陣営へ、その倒しをアシストするたびにアシストした人の陣営へ、決まった点を足す。時間では点を足さない。
    /// </summary>
    public sealed class KillScore : IScoreRule
    {
        /// <summary>1 キルの点。0 より大きい有限値。</summary>
        public double PointsPerKill { get; }

        /// <summary>1 アシストの点。0 以上の有限値。0 ならアシストでは点が入らない。</summary>
        public double PointsPerAssist { get; }

        /// <param name="pointsPerKill">1 キルの点。0 より大きい有限値。</param>
        /// <param name="pointsPerAssist">1 アシストの点。0 以上の有限値。</param>
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

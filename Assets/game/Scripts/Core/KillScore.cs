using System;

namespace Fps.Core
{
    /// <summary>別の陣営を倒すたびに、倒した人の陣営へ決まった点を足す。時間では点を足さない。</summary>
    public sealed class KillScore : IScoreRule
    {
        /// <summary>1 キルの点。0 より大きい有限値。</summary>
        public double PointsPerKill { get; }

        /// <param name="pointsPerKill">1 キルの点。0 より大きい有限値。</param>
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

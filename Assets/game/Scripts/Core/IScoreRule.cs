namespace Shooter.Core
{
    public interface IScoreRule
    {
        double PointsForKill(ModeSession session, int killerId, int victimId);

        double PointsForAssist(ModeSession session, int assisterId, int victimId);

        double PointsPerSecond(ModeSession session, Side side);

        void Advance(ModeSession session, float seconds);
    }
}

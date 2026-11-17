namespace Shooter.Core
{
    public readonly struct TeamMove
    {
        public TeamMove(int playerId, Team from, Team to)
        {
            PlayerId = playerId;
            From = from;
            To = to;
        }

        public int PlayerId { get; }

        public Team From { get; }

        public Team To { get; }
    }
}

namespace Shooter.Core
{
    /// <summary><see cref="TeamBalancer.Plan"/> が決めた、1 人をどのチームからどのチームへ移すか。</summary>
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

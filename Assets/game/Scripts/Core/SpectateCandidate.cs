namespace Shooter.Core
{
    public readonly struct SpectateCandidate
    {
        public SpectateCandidate(ulong id, Team team, bool isAlive)
        {
            Id = id;
            Team = team;
            IsAlive = isAlive;
        }

        public ulong Id { get; }

        public Team Team { get; }

        public bool IsAlive { get; }
    }
}

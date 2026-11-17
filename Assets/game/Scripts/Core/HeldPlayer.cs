namespace Shooter.Core
{
    public readonly struct HeldPlayer
    {
        public HeldPlayer(int playerId, Team team, int kills, int deaths, float remainingSeconds)
        {
            PlayerId = playerId;
            Team = team;
            Kills = kills;
            Deaths = deaths;
            RemainingSeconds = remainingSeconds;
        }

        public int PlayerId { get; }

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public float RemainingSeconds { get; }

        internal HeldPlayer WithRemaining(float remainingSeconds) => new HeldPlayer(PlayerId, Team, Kills, Deaths, remainingSeconds);

        internal HeldPlayer WithoutStats() => new HeldPlayer(PlayerId, Team, 0, 0, RemainingSeconds);
    }
}

namespace Shooter.Core
{
    /// <summary>
    /// 切断した人の、戻ってきたら引き継ぐ成績。<see cref="TeamDeathmatchSession.FindHeld"/> が返す。
    /// </summary>
    public readonly struct HeldPlayer
    {
        public HeldPlayer(int playerId, Team team, int kills, int deaths, float remainingSeconds)
            : this(playerId, team, kills, deaths, 0, remainingSeconds)
        {
        }

        public HeldPlayer(int playerId, Team team, int kills, int deaths, int assists, float remainingSeconds)
        {
            PlayerId = playerId;
            Team = team;
            Kills = kills;
            Deaths = deaths;
            Assists = assists;
            RemainingSeconds = remainingSeconds;
        }

        /// <summary>切断する前の playerId。</summary>
        public int PlayerId { get; }

        public Team Team { get; }

        public int Kills { get; }

        public int Deaths { get; }

        public int Assists { get; }

        /// <summary>この秒数のうちに戻らなければ捨てる。</summary>
        public float RemainingSeconds { get; }

        internal HeldPlayer WithRemaining(float remainingSeconds) => new HeldPlayer(PlayerId, Team, Kills, Deaths, Assists, remainingSeconds);

        internal HeldPlayer WithoutStats() => new HeldPlayer(PlayerId, Team, 0, 0, 0, RemainingSeconds);
    }
}

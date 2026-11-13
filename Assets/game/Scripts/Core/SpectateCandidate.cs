namespace Fps.Core
{
    /// <summary><see cref="SpectateTargets.Pick"/> に渡す、観戦で映せるかもしれないプレイヤー 1 人分。</summary>
    public readonly struct SpectateCandidate
    {
        /// <param name="id">プレイヤーを見分ける Id。ネット対戦ではクライアント Id。並び順にも使う。</param>
        /// <param name="team">チーム。</param>
        /// <param name="isAlive">生きているか (ダウン中も生きている)。</param>
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

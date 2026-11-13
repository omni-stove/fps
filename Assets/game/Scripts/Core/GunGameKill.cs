namespace Fps.Core
{
    /// <summary><see cref="GunGame.ReportKill"/> で 1 回のキルが段をどう動かしたか。段は 0 から数え、勝った人は段の数と同じ値。</summary>
    public readonly struct GunGameKill
    {
        public GunGameKill(int killerId, int killerTierBefore, int killerTier, int victimId, int victimTierBefore, int victimTier, bool finished)
        {
            KillerId = killerId;
            KillerTierBefore = killerTierBefore;
            KillerTier = killerTier;
            VictimId = victimId;
            VictimTierBefore = victimTierBefore;
            VictimTier = victimTier;
            Finished = finished;
        }

        public int KillerId { get; }
        public int KillerTierBefore { get; }
        public int KillerTier { get; }
        public int VictimId { get; }
        public int VictimTierBefore { get; }
        public int VictimTier { get; }

        /// <summary>倒した人が最後の段の武器で倒し、勝ちを決めたら true。</summary>
        public bool Finished { get; }

        /// <summary>倒した人が次の段へ進んだら true (最後の段を終えたときも)。</summary>
        public bool Promoted => KillerTier > KillerTierBefore;

        /// <summary>倒された人が 1 段戻ったら true。</summary>
        public bool Demoted => VictimTier < VictimTierBefore;
    }
}

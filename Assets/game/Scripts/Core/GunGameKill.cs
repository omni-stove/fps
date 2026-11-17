namespace Shooter.Core
{
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

        public bool Finished { get; }

        public bool Promoted => KillerTier > KillerTierBefore;

        public bool Demoted => VictimTier < VictimTierBefore;
    }
}

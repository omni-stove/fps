namespace Shooter.Core
{
    // オフラインでは進んでいる SearchAndDestroy そのもの、対戦では届いたスナップショットから組み立てた写しが、同じ形で HUD に渡る。
    public interface ISearchAndDestroyView
    {
        RoundPhase Phase { get; }

        int RoundNumber { get; }

        int WinsToWin { get; }

        float PreparationRemaining { get; }

        Side Attacker { get; }

        Side Defender { get; }

        Side? Winner { get; }

        BombState Bomb { get; }

        int? CarrierId { get; }

        int? PlantingSite { get; }

        float PlantFraction { get; }

        int? PlantedSite { get; }

        int? DefuserId { get; }

        float DefuseFraction { get; }

        float RoundRemaining { get; }

        float FuseRemaining { get; }

        RoundEndReason? LastRoundEnd { get; }

        Side? LastRoundWinner { get; }

        int GetRoundWins(Side side);

        bool HasPlayer(int playerId);

        bool IsAttacker(int playerId);
    }
}

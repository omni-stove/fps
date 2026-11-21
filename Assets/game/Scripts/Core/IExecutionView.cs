namespace Shooter.Core
{
    // HUD が ExecutionSession そのものでなくこれを読むのは、ネット対戦のクライアントがスナップショットから同じ形を作れるようにするため (ISearchAndDestroyView と同じ)
    public interface IExecutionView
    {
        RoundPhase Phase { get; }

        // 始まる前は 0
        int RoundNumber { get; }

        int WinsToWin { get; }

        float PreparationRemaining { get; }

        float RoundRemaining { get; }

        bool HasFinishedRound { get; }

        // 引き分けなら null
        Side? LastRoundWinner { get; }

        // 試合が終わっていない、または引き分けなら null
        Side? Winner { get; }

        int GetRoundWins(Side side);

        bool HasPlayer(int playerId);

        Side GetSide(int playerId);

        // ダウン中の人も数える
        int CountRemaining(Side side);

        int CountDowned(Side side);

        bool IsDowned(int playerId);

        float? GetBleedoutRemaining(int playerId);

        float GetReviveProgress(int playerId);

        float GetExecuteProgress(int playerId);
    }
}

using System.Collections.Generic;

namespace Shooter.Core
{
    // オフラインでは進んでいる BattleRoyaleSession そのもの、対戦では届いたスナップショットから組み立てた写しが、同じ形で HUD に渡る。
    public interface IBattleRoyaleView
    {
        GameModeState State { get; }

        bool HasTimeLimit { get; }

        // 制限時間が無ければ 0
        float RemainingSeconds { get; }

        double ElapsedSeconds { get; }

        // 終わる前と引き分けは null
        Side? Winner { get; }

        IReadOnlyList<Side> Sides { get; }

        int EntrantCount { get; }

        int SquadsRemaining { get; }

        bool HasPlayer(int playerId);

        Side GetSide(int playerId);

        int CountMembers(Side squad);

        int CountRemaining(Side squad);

        int CountStanding(Side squad);

        int CountDowned(Side squad);

        int? GetPlacement(Side squad);

        bool IsDowned(int playerId);

        bool IsStanding(int playerId);

        float? GetBleedoutRemaining(int playerId);

        float GetReviveProgress(int playerId);
    }
}

using System;

namespace Shooter.Core
{
    public interface IGameMode
    {
        event Action<GameModeState> StateChanged;

        GameModeState State { get; }

        void Start();

        void Tick(float deltaSeconds);

        void ReportKill(int killerId, int victimId);

        void ReportDeath(int playerId);
    }
}

using System;

namespace Shooter.Core
{
    // Game は写しでなく進んでいる試合そのものなので、受け取った側は出された時に読む。
    public sealed class SearchAndDestroyStatus
    {
        public SearchAndDestroyStatus(SearchAndDestroy game, int? localPlayerId)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            LocalPlayerId = localPlayerId;
        }

        public SearchAndDestroy Game { get; }

        public int? LocalPlayerId { get; }
    }
}

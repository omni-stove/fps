using System;

namespace Shooter.Core
{
    public sealed class GunGameStatus
    {
        public GunGameStatus(GunGame gunGame, int? localPlayerId)
        {
            GunGame = gunGame ?? throw new ArgumentNullException(nameof(gunGame));
            LocalPlayerId = localPlayerId;
        }

        public GunGame GunGame { get; }

        public int? LocalPlayerId { get; }
    }
}

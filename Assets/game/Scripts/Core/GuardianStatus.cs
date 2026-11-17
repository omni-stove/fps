using System;

namespace Shooter.Core
{
    public sealed class GuardianStatus
    {
        public GuardianStatus(GuardianRespawn leaders, int? localPlayerId, bool isLocalPlayerEliminated)
        {
            Leaders = leaders ?? throw new ArgumentNullException(nameof(leaders));
            LocalPlayerId = localPlayerId;
            IsLocalPlayerEliminated = localPlayerId.HasValue && isLocalPlayerEliminated;
        }

        public GuardianRespawn Leaders { get; }

        public int? LocalPlayerId { get; }

        public bool IsLocalPlayerEliminated { get; }
    }
}

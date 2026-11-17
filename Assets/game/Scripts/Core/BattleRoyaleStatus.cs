using System;

namespace Shooter.Core
{
    public sealed class BattleRoyaleStatus
    {
        public BattleRoyaleStatus(BattleRoyaleSession session, int? localPlayerId, int? reviveTargetId, ZoneState? zone, bool isLocalPlayerUnarmed)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            LocalPlayerId = localPlayerId;
            ReviveTargetId = localPlayerId.HasValue ? reviveTargetId : null;
            Zone = zone;
            IsLocalPlayerUnarmed = localPlayerId.HasValue && isLocalPlayerUnarmed;
        }

        public BattleRoyaleSession Session { get; }

        public int? LocalPlayerId { get; }

        public int? ReviveTargetId { get; }

        public ZoneState? Zone { get; }

        public bool IsLocalPlayerUnarmed { get; }
    }
}

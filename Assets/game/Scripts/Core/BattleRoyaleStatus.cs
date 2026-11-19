using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class BattleRoyaleStatus
    {
        public BattleRoyaleStatus(
            BattleRoyaleSession session,
            int? localPlayerId,
            int? reviveTargetId,
            ZoneState? zone,
            bool isLocalPlayerUnarmed,
            IReadOnlyList<SupplyDropMark> supplyDrops = null,
            bool isLocalPlayerAtSupplyDrop = false,
            string interactKeyLabel = null)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
            LocalPlayerId = localPlayerId;
            ReviveTargetId = localPlayerId.HasValue ? reviveTargetId : null;
            Zone = zone;
            IsLocalPlayerUnarmed = localPlayerId.HasValue && isLocalPlayerUnarmed;
            SupplyDrops = supplyDrops == null ? Array.Empty<SupplyDropMark>() : Copy(supplyDrops);
            IsLocalPlayerAtSupplyDrop = localPlayerId.HasValue && isLocalPlayerAtSupplyDrop;
            InteractKeyLabel = interactKeyLabel;

            if (localPlayerId.HasValue && session.Mode.HasPlayer(localPlayerId.Value))
            {
                LocalRedeploySeconds = session.GetRedeployRemaining(localPlayerId.Value);
                var squadOut = session.GetPlacement(session.Mode.GetSide(localPlayerId.Value)).HasValue;
                var downedWithoutReturn = session.Mode.IsDown(localPlayerId.Value) && !LocalRedeploySeconds.HasValue;
                LocalRedeploysLeft = (session.IsRedeployOpen && !squadOut && !downedWithoutReturn)
                    || LocalRedeploySeconds.HasValue
                    ? session.GetRedeploysLeft(localPlayerId.Value)
                    : (int?)null;
            }

            foreach (var drop in SupplyDrops)
            {
                if (drop.HasLanded)
                {
                    LandedSupplyDrops++;
                }
                else
                {
                    FallingSupplyDrops++;
                }
            }
        }

        public BattleRoyaleSession Session { get; }

        public int? LocalPlayerId { get; }

        public int? ReviveTargetId { get; }

        public ZoneState? Zone { get; }

        public bool IsLocalPlayerUnarmed { get; }

        // まだ開けられていない箱 (落ちている途中の物も含む)。開けた箱は載せない。
        public IReadOnlyList<SupplyDropMark> SupplyDrops { get; }

        public int FallingSupplyDrops { get; }

        public int LandedSupplyDrops { get; }

        public bool IsLocalPlayerAtSupplyDrop { get; }

        public string InteractKeyLabel { get; }

        public float? LocalRedeploySeconds { get; }

        public int? LocalRedeploysLeft { get; }

        private static SupplyDropMark[] Copy(IReadOnlyList<SupplyDropMark> drops)
        {
            var copy = new SupplyDropMark[drops.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = drops[i];
            }

            return copy;
        }
    }
}

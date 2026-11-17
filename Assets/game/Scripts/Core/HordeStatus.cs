using System;

namespace Shooter.Core
{
    public sealed class HordeStatus
    {
        public HordeStatus(Horde horde, int? localPlayerId, HordeFortificationOffer? offer)
        {
            Horde = horde ?? throw new ArgumentNullException(nameof(horde));
            LocalPlayerId = localPlayerId;
            Offer = localPlayerId.HasValue ? offer : null;
        }

        public Horde Horde { get; }

        public int? LocalPlayerId { get; }

        public HordeFortificationOffer? Offer { get; }
    }
}

using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public static class ClientKillFeed
    {
        public static KillFeedEntry FromMessage(KilledMessage killed)
        {
            if (killed == null)
            {
                throw new ArgumentNullException(nameof(killed));
            }

            var assisters = killed.AssisterCount == 0 ? Array.Empty<int>() : new int[killed.AssisterCount];
            for (var i = 0; i < assisters.Length; i++)
            {
                assisters[i] = killed.AssisterId(i);
            }

            return new KillFeedEntry(killed.KillerId, killed.VictimId, killed.WeaponId, killed.Headshot, killed.Friendly, assisters);
        }
    }
}

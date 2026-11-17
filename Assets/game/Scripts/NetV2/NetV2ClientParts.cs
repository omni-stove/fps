using System;
using System.Collections.Generic;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    internal static class NetV2ClientParts
    {
        public static List<INetV2ClientPart> Create(MatchClient client, Func<IMessage, bool> send)
        {
            if (client == null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (send == null)
            {
                throw new ArgumentNullException(nameof(send));
            }

            return new List<INetV2ClientPart>();
        }
    }
}

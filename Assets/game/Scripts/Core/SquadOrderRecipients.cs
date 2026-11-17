using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public static class SquadOrderRecipients
    {
        public static int Select(
            Side side,
            Vector3 position,
            bool hasSquads,
            float nearbyRadius,
            IReadOnlyList<SquadOrderCandidate> candidates,
            List<int> recipients)
        {
            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (recipients == null)
            {
                throw new ArgumentNullException(nameof(recipients));
            }

            if (!(nearbyRadius > 0f) || float.IsInfinity(nearbyRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(nearbyRadius), nearbyRadius, "nearbyRadius must be positive and finite");
            }

            recipients.Clear();
            var here = new Vector2(position.X, position.Z);
            var limit = nearbyRadius * nearbyRadius;
            for (var i = 0; i < candidates.Count; i++)
            {
                var candidate = candidates[i];
                if (!candidate.IsAlive || candidate.Side != side)
                {
                    continue;
                }

                if (!hasSquads)
                {
                    var distance = Vector2.DistanceSquared(here, new Vector2(candidate.Position.X, candidate.Position.Z));
                    if (!(distance <= limit))
                    {
                        continue;
                    }
                }

                recipients.Add(i);
            }

            return recipients.Count;
        }
    }
}

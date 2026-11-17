using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerHitboxes
    {
        private static readonly Comparer<ShotHit> _nearestFirst = Comparer<ShotHit>.Create((a, b) => a.Distance.CompareTo(b.Distance));

        // 火炎は人を素通りして円錐の中の全員に届くので、Cast のように線の上で近い順に止めず、相手ごとに円錐と世界の遮りを確かめる。
        public void Spray(
            Vector3 origin, Vector3 direction, SpraySpec spray, float range, float tick, int shooterId, Func<int, bool> hittable, List<ShotHit> hits)
        {
            ArgumentNullException.ThrowIfNull(spray);
            ArgumentNullException.ThrowIfNull(hits);
            Validate(origin, direction, range, hittable);
            if (!HasHistory)
            {
                return;
            }

            var unit = Vector3.Normalize(direction);
            var clamped = _lag.ClampTick(tick, LastRecordedTick);
            var start = hits.Count;
            foreach (var (id, body) in RewindBodies(clamped, shooterId, hittable))
            {
                var point = SprayCone.NearestPoint(range, origin, unit, body.Bottom, body.Top, body.Radius);
                if (!SprayCone.Contains(spray, range, origin, unit, point))
                {
                    continue;
                }

                var toPoint = point - origin;
                var distance = toPoint.Length();
                var toward = distance > 1e-4f ? toPoint / distance : unit;
                if (distance > 1e-4f
                    && (_world.Raycast(origin, toward, distance, RaycastLayers, out _)
                        || (_vehicles != null && _vehicles.BlocksLine(origin, point, clamped, shooterId))))
                {
                    continue;
                }

                var hit = ShotHit.Target(point, -toward, distance, id, body.Position, body.Crouched, body.Zone(point));
                hits.Add(hit.WithRewind(LastRecordedTick - clamped));
            }

            hits.Sort(start, hits.Count - start, _nearestFirst);
        }
    }
}

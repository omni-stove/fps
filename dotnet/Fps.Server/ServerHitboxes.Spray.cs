using System;
using System.Collections.Generic;
using System.Numerics;
using Fps.Core;

namespace Fps.Server
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
            var rewound = _lag.Rewind(clamped);
            var crouched = _lag.RewindCrouched(clamped);
            _ids.Clear();
            foreach (var body in _bodies.All)
            {
                _ids.Add(body.PlayerId);
            }

            var start = hits.Count;
            var geometry = _bodies.Geometry;
            foreach (var id in _ids)
            {
                if (id == shooterId || !rewound.TryGetValue(id, out var position) || !hittable(id))
                {
                    continue;
                }

                var isCrouched = crouched.TryGetValue(id, out var c) && c;
                var shape = geometry.Shape(isCrouched);
                geometry.SphereCenters(shape, out var bottomY, out var topY);
                var point = SprayCone.NearestPoint(
                    range, origin, unit, position + new Vector3(0f, bottomY, 0f), position + new Vector3(0f, topY, 0f), geometry.Radius);
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

                var bottom = position.Y + shape.CenterY - (shape.Height / 2f);
                var hit = ShotHit.Target(point, -toward, distance, id, position, isCrouched, Zone(point.Y, bottom, shape.Height));
                hits.Add(hit.WithRewind(LastRecordedTick - clamped));
            }

            hits.Sort(start, hits.Count - start, _nearestFirst);
        }
    }
}

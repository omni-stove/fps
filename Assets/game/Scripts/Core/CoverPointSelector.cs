using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class CoverPointSelector
    {
        public const float DefaultMaxDistance = 15f;

        public const float DefaultMaxCoverAngleDegrees = 60f;

        // 脅威が点のすぐそばにいると壁越しでも撃たれるので、この水平距離 (m) より近ければ遮っているとみなさない。
        public const float DefaultMinThreatDistance = 2f;

        // 角度の境界ちょうどを float の丸めで落とさないための余裕。
        private const float _boundaryEpsilon = 1e-4f;

        private readonly CoverPoint[] _points;
        private readonly object[] _occupants;
        private readonly float _minCoverCos;

        public CoverPointSelector(
            IReadOnlyList<CoverPoint> points,
            float maxDistance = DefaultMaxDistance,
            float maxCoverAngleDegrees = DefaultMaxCoverAngleDegrees,
            float minThreatDistance = DefaultMinThreatDistance)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            if (!(maxDistance > 0f) || float.IsInfinity(maxDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must be positive and finite");
            }

            if (!(maxCoverAngleDegrees >= 0f) || maxCoverAngleDegrees > 180f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxCoverAngleDegrees), maxCoverAngleDegrees, "maxCoverAngleDegrees must be between 0 and 180");
            }

            if (!(minThreatDistance >= 0f) || float.IsInfinity(minThreatDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(minThreatDistance), minThreatDistance, "minThreatDistance must be non-negative and finite");
            }

            _points = new CoverPoint[points.Count];
            for (var i = 0; i < _points.Length; i++)
            {
                _points[i] = points[i];
            }

            Points = Array.AsReadOnly(_points);
            _occupants = new object[_points.Length];
            _minCoverCos = (float)Math.Cos(maxCoverAngleDegrees * (Math.PI / 180.0));
            MaxDistance = maxDistance;
            MaxCoverAngleDegrees = maxCoverAngleDegrees;
            MinThreatDistance = minThreatDistance;
        }

        public IReadOnlyList<CoverPoint> Points { get; }

        public float MaxDistance { get; }

        public float MaxCoverAngleDegrees { get; }

        public float MinThreatDistance { get; }

        public bool Protects(int index, Vector3 threat)
        {
            RequireIndex(index);
            RequireFinite(threat, nameof(threat));
            return ProtectsUnchecked(_points[index], threat);
        }

        public int Select(Vector3 self, Vector3 threat, object owner = null, ICollection<int> excluded = null)
        {
            RequireFinite(self, nameof(self));
            RequireFinite(threat, nameof(threat));

            var best = -1;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < _points.Length; i++)
            {
                var occupant = _occupants[i];
                if (occupant != null && !ReferenceEquals(occupant, owner))
                {
                    continue;
                }

                if (excluded != null && excluded.Contains(i))
                {
                    continue;
                }

                var distance = Vector3.Distance(self, _points[i].Position);
                if (distance > MaxDistance || distance >= bestDistance)
                {
                    continue;
                }

                if (!ProtectsUnchecked(_points[i], threat))
                {
                    continue;
                }

                best = i;
                bestDistance = distance;
            }

            return best;
        }

        public bool TryClaim(int index, object owner)
        {
            if (owner == null)
            {
                throw new ArgumentNullException(nameof(owner));
            }

            RequireIndex(index);
            var occupant = _occupants[index];
            if (occupant != null && !ReferenceEquals(occupant, owner))
            {
                return false;
            }

            Release(owner);
            _occupants[index] = owner;
            return true;
        }

        public bool Release(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            var released = false;
            for (var i = 0; i < _occupants.Length; i++)
            {
                if (ReferenceEquals(_occupants[i], owner))
                {
                    _occupants[i] = null;
                    released = true;
                }
            }

            return released;
        }

        public bool IsOccupied(int index)
        {
            RequireIndex(index);
            return _occupants[index] != null;
        }

        public int IndexClaimedBy(object owner)
        {
            if (owner == null)
            {
                return -1;
            }

            for (var i = 0; i < _occupants.Length; i++)
            {
                if (ReferenceEquals(_occupants[i], owner))
                {
                    return i;
                }
            }

            return -1;
        }

        private bool ProtectsUnchecked(CoverPoint point, Vector3 threat)
        {
            var toThreat = threat - point.Position;
            toThreat.Y = 0f;
            var distance = toThreat.Length();
            if (distance < MinThreatDistance || !(distance > 0f))
            {
                return false;
            }

            return Vector3.Dot(point.Facing, toThreat / distance) >= _minCoverCos - _boundaryEpsilon;
        }

        private void RequireIndex(int index)
        {
            if (index < 0 || index >= _points.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index), index, "index must be within the cover points");
            }
        }

        private static void RequireFinite(Vector3 value, string name)
        {
            if (!CoverPoint.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be finite");
            }
        }
    }
}

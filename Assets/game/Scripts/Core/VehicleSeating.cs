using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class VehicleSeating
    {
        private readonly object[] _occupants;

        public VehicleSeating(int seatCount)
        {
            if (seatCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "seatCount must be at least 1");
            }

            _occupants = new object[seatCount];
        }

        public int SeatCount => _occupants.Length;

        public int OccupiedCount
        {
            get
            {
                var count = 0;
                foreach (var occupant in _occupants)
                {
                    if (occupant != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsOccupied(int seat)
        {
            RequireSeat(seat);
            return _occupants[seat] != null;
        }

        public object OccupantOf(int seat)
        {
            RequireSeat(seat);
            return _occupants[seat];
        }

        public int SeatOf(object occupant)
        {
            if (occupant == null)
            {
                throw new ArgumentNullException(nameof(occupant));
            }

            for (var i = 0; i < _occupants.Length; i++)
            {
                if (ReferenceEquals(_occupants[i], occupant))
                {
                    return i;
                }
            }

            return -1;
        }

        public bool Board(object occupant, int seat)
        {
            RequireSeat(seat);
            if (_occupants[seat] != null || SeatOf(occupant) >= 0)
            {
                return false;
            }

            _occupants[seat] = occupant;
            return true;
        }

        public int Leave(object occupant)
        {
            var seat = SeatOf(occupant);
            if (seat >= 0)
            {
                _occupants[seat] = null;
            }

            return seat;
        }

        public bool MoveTo(object occupant, int seat)
        {
            RequireSeat(seat);
            var from = SeatOf(occupant);
            if (from < 0 || from == seat || _occupants[seat] != null)
            {
                return false;
            }

            _occupants[from] = null;
            _occupants[seat] = occupant;
            return true;
        }

        public int NextFree(int from, int direction)
        {
            if (from < -1 || from >= _occupants.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(from), from, "from must be -1 or a seat index");
            }

            if (direction != 1 && direction != -1)
            {
                throw new ArgumentOutOfRangeException(nameof(direction), direction, "direction must be 1 or -1");
            }

            var count = _occupants.Length;
            var start = from >= 0 ? from : (direction > 0 ? count - 1 : 0);
            for (var step = 1; step <= count; step++)
            {
                var seat = (((start + (direction * step)) % count) + count) % count;
                if (seat != from && _occupants[seat] == null)
                {
                    return seat;
                }
            }

            return -1;
        }

        public int NearestFree(IReadOnlyList<float> distances, float maxDistance)
        {
            if (distances == null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            if (distances.Count != _occupants.Length)
            {
                throw new ArgumentException(
                    $"distances has {distances.Count} entries but there are {_occupants.Length} seats", nameof(distances));
            }

            if (!(maxDistance >= 0f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDistance), maxDistance, "maxDistance must not be negative");
            }

            var best = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < _occupants.Length; i++)
            {
                var distance = distances[i];
                if (_occupants[i] != null || float.IsNaN(distance) || distance > maxDistance)
                {
                    continue;
                }

                if (best < 0 || distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            return best;
        }

        public IReadOnlyList<object> Clear()
        {
            var left = new List<object>();
            for (var i = 0; i < _occupants.Length; i++)
            {
                if (_occupants[i] != null)
                {
                    left.Add(_occupants[i]);
                    _occupants[i] = null;
                }
            }

            return left;
        }

        private void RequireSeat(int seat)
        {
            if (seat < 0 || seat >= _occupants.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(seat), seat, $"seat must be between 0 and {_occupants.Length - 1}");
            }
        }
    }
}

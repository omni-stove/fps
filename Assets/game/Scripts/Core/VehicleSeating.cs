using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 乗り物のどの座席に誰が座っているか。座る・降りる・移る と、空いている座席の選び方 (一番近い・次の) を受け持つ。
    /// 乗っている人は参照の同一性で区別する。1 人が座れるのは 1 つの座席だけ。
    /// </summary>
    public sealed class VehicleSeating
    {
        private readonly object[] _occupants;

        /// <param name="seatCount">座席の数。1 以上。</param>
        public VehicleSeating(int seatCount)
        {
            if (seatCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(seatCount), seatCount, "seatCount must be at least 1");
            }

            _occupants = new object[seatCount];
        }

        public int SeatCount => _occupants.Length;

        /// <summary>座っている人数。</summary>
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

        /// <summary>seat に座っている人。空いていれば null。</summary>
        public object OccupantOf(int seat)
        {
            RequireSeat(seat);
            return _occupants[seat];
        }

        /// <summary>occupant が座っている座席。座っていなければ -1。</summary>
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

        /// <summary>occupant を seat に座らせる。seat が塞がっているか、occupant がもう座っていれば何もせず false。</summary>
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

        /// <summary>occupant を降ろし、座っていた座席を返す。座っていなければ -1。</summary>
        public int Leave(object occupant)
        {
            var seat = SeatOf(occupant);
            if (seat >= 0)
            {
                _occupants[seat] = null;
            }

            return seat;
        }

        /// <summary>
        /// 座っている occupant を seat へ移す。occupant が座っていない・seat が今の座席・seat が塞がっていれば何もせず false。
        /// </summary>
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

        /// <summary>
        /// from から direction の向きに巡って最初の空いている座席。from 自身は選ばない。空きが無ければ -1。
        /// from が -1 なら、direction が正のとき 0 番から、負のとき最後の番から探す。
        /// </summary>
        /// <param name="from">探し始める座席。-1 か座席の番号。</param>
        /// <param name="direction">1 なら番号の大きいほう、-1 なら小さいほうへ巡る。</param>
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

        /// <summary>
        /// 空いている座席のうち、乗る位置までの距離 (distances の同じ番号) が maxDistance 以内で一番近いもの。同じ距離なら番号の小さいほう。
        /// 距離が NaN の座席は届かないものとして選ばない。届く空席が無ければ -1。
        /// </summary>
        /// <param name="distances">座席ごとの距離 (m)。座席の数と同じ長さ。</param>
        /// <param name="maxDistance">乗り込める距離 (m)。0 以上。</param>
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

        /// <summary>全員を降ろし、座っていた人を座席の順に返す。</summary>
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

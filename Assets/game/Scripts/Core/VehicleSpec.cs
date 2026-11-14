using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 乗り物 1 台の設定。耐久・部位・座席の並び・武器・乗り込める距離・追うカメラの置き方を持つ。生成時に値を検査し、以後は変わらない。
    /// 座席の番号は <see cref="Seats"/> の添字で、シーンの座席の部品の並びと対応させる。
    /// </summary>
    public sealed class VehicleSpec
    {
        public const string DefaultId = "vehicle";

        public const int DefaultMaxHealth = 1000;

        /// <summary>座席の乗る位置からこの距離 (m) までなら乗り込める。</summary>
        public const float DefaultEnterRange = 2.5f;

        /// <summary>追うカメラを、注視点からどれだけ離すか (m)。</summary>
        public const float DefaultChaseDistance = 6f;

        /// <summary>追うカメラの注視点を、乗り物の原点からどれだけ上に置くか (m)。</summary>
        public const float DefaultChaseHeight = 2f;

        private readonly VehicleSeatSpec[] _seats;
        private readonly VehicleWeaponSpec[] _weapons;
        private readonly VehiclePartSpec[] _parts;

        /// <param name="id">乗り物の Id。空でない。</param>
        /// <param name="maxHealth">耐久。1 以上。</param>
        /// <param name="seats">座席の並び。1 つ以上で null を含まず、運転席は 1 つまで。</param>
        /// <param name="weapons">武器。null なら無し。null を含まず、操作する座席は seats の範囲。</param>
        /// <param name="parts">部位。null なら無し。null を含まず、Id は重ならない。</param>
        /// <param name="enterRange">乗り込める距離 (m)。0 より大きい有限値。</param>
        /// <param name="chaseDistance">追うカメラの注視点からの距離 (m)。0 以上の有限値。</param>
        /// <param name="chaseHeight">追うカメラの注視点の高さ (m)。有限値。</param>
        public VehicleSpec(
            string id,
            int maxHealth,
            IReadOnlyList<VehicleSeatSpec> seats,
            IReadOnlyList<VehicleWeaponSpec> weapons = null,
            IReadOnlyList<VehiclePartSpec> parts = null,
            float enterRange = DefaultEnterRange,
            float chaseDistance = DefaultChaseDistance,
            float chaseHeight = DefaultChaseHeight)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (maxHealth < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxHealth), maxHealth, "maxHealth must be at least 1");
            }

            if (seats == null)
            {
                throw new ArgumentNullException(nameof(seats));
            }

            if (seats.Count == 0)
            {
                throw new ArgumentException("a vehicle needs at least one seat", nameof(seats));
            }

            if (!(enterRange > 0f) || float.IsInfinity(enterRange))
            {
                throw new ArgumentOutOfRangeException(nameof(enterRange), enterRange, "enterRange must be positive and finite");
            }

            if (!(chaseDistance >= 0f) || float.IsInfinity(chaseDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(chaseDistance), chaseDistance, "chaseDistance must not be negative and must be finite");
            }

            if (float.IsNaN(chaseHeight) || float.IsInfinity(chaseHeight))
            {
                throw new ArgumentOutOfRangeException(nameof(chaseHeight), chaseHeight, "chaseHeight must be finite");
            }

            _seats = CopySeats(seats, out var driverSeatIndex);
            _weapons = CopyWeapons(weapons, _seats.Length);
            _parts = CopyParts(parts);

            Id = id;
            MaxHealth = maxHealth;
            DriverSeatIndex = driverSeatIndex;
            EnterRange = enterRange;
            ChaseDistance = chaseDistance;
            ChaseHeight = chaseHeight;
        }

        public string Id { get; }

        public int MaxHealth { get; }

        public IReadOnlyList<VehicleSeatSpec> Seats => _seats;

        public IReadOnlyList<VehicleWeaponSpec> Weapons => _weapons;

        public IReadOnlyList<VehiclePartSpec> Parts => _parts;

        /// <summary>運転席の番号。運転席が無ければ -1 (固定銃座など)。</summary>
        public int DriverSeatIndex { get; }

        public float EnterRange { get; }

        public float ChaseDistance { get; }

        public float ChaseHeight { get; }

        /// <summary>seatIndex の座席が操作する武器の番号 (<see cref="Weapons"/> の添字) を並びの順に返す。</summary>
        public IReadOnlyList<int> WeaponsOfSeat(int seatIndex)
        {
            if (seatIndex < 0 || seatIndex >= _seats.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(seatIndex), seatIndex, "seatIndex is outside the seats");
            }

            var result = new List<int>();
            for (var i = 0; i < _weapons.Length; i++)
            {
                if (_weapons[i].SeatIndex == seatIndex)
                {
                    result.Add(i);
                }
            }

            return result;
        }

        /// <summary>id の部位。無ければ null。</summary>
        public VehiclePartSpec FindPart(string id)
        {
            foreach (var part in _parts)
            {
                if (string.Equals(part.Id, id, StringComparison.Ordinal))
                {
                    return part;
                }
            }

            return null;
        }

        private static VehicleSeatSpec[] CopySeats(IReadOnlyList<VehicleSeatSpec> seats, out int driverSeatIndex)
        {
            driverSeatIndex = -1;
            var copy = new VehicleSeatSpec[seats.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var seat = seats[i] ?? throw new ArgumentException($"seats[{i}] is null", nameof(seats));
                if (seat.Role == VehicleSeatRole.Driver)
                {
                    if (driverSeatIndex >= 0)
                    {
                        throw new ArgumentException(
                            $"a vehicle has at most one driver seat (seats[{driverSeatIndex}] and seats[{i}])", nameof(seats));
                    }

                    driverSeatIndex = i;
                }

                copy[i] = seat;
            }

            return copy;
        }

        private static VehicleWeaponSpec[] CopyWeapons(IReadOnlyList<VehicleWeaponSpec> weapons, int seatCount)
        {
            if (weapons == null)
            {
                return Array.Empty<VehicleWeaponSpec>();
            }

            var copy = new VehicleWeaponSpec[weapons.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                var weapon = weapons[i] ?? throw new ArgumentException($"weapons[{i}] is null", nameof(weapons));
                if (weapon.SeatIndex >= seatCount)
                {
                    throw new ArgumentException(
                        $"weapons[{i}] is operated from seat {weapon.SeatIndex} but the vehicle has {seatCount} seats", nameof(weapons));
                }

                copy[i] = weapon;
            }

            return copy;
        }

        private static VehiclePartSpec[] CopyParts(IReadOnlyList<VehiclePartSpec> parts)
        {
            if (parts == null)
            {
                return Array.Empty<VehiclePartSpec>();
            }

            var copy = new VehiclePartSpec[parts.Count];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < copy.Length; i++)
            {
                var part = parts[i] ?? throw new ArgumentException($"parts[{i}] is null", nameof(parts));
                if (!ids.Add(part.Id))
                {
                    throw new ArgumentException($"part id '{part.Id}' appears more than once", nameof(parts));
                }

                copy[i] = part;
            }

            return copy;
        }
    }
}

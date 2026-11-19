using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 座席の番号は Seats の添字で、シーンの座席の部品の並びと対応させる。
    public sealed class VehicleSpec
    {
        public const string DefaultId = "vehicle";

        public const string ExplosionWeaponId = "vehicle";

        public const int DefaultMaxHealth = 1000;

        public const float DefaultEnterRange = 2.5f;

        public const float DefaultChaseDistance = 6f;

        public const float DefaultChaseHeight = 2f;

        // キットの既定は今までどおりすぐ乗り降りする。時間を掛けるかはゲームが決める。
        public const float DefaultEnterSeconds = 0f;

        public const float DefaultExitSeconds = 0f;

        private readonly VehicleSeatSpec[] _seats;
        private readonly VehicleWeaponSpec[] _weapons;
        private readonly VehiclePartSpec[] _parts;

        public VehicleSpec(
            string id,
            int maxHealth,
            IReadOnlyList<VehicleSeatSpec> seats,
            IReadOnlyList<VehicleWeaponSpec> weapons = null,
            IReadOnlyList<VehiclePartSpec> parts = null,
            float enterRange = DefaultEnterRange,
            float chaseDistance = DefaultChaseDistance,
            float chaseHeight = DefaultChaseHeight,
            VehicleDurabilitySpec durability = null,
            VehicleSpeedFieldOfViewSpec speedFieldOfView = null,
            BailOutSpec bailOut = null,
            float enterSeconds = DefaultEnterSeconds,
            float exitSeconds = DefaultExitSeconds)
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

            if (!(enterSeconds >= 0f) || float.IsInfinity(enterSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(enterSeconds), enterSeconds, "enterSeconds must not be negative and must be finite");
            }

            if (!(exitSeconds >= 0f) || float.IsInfinity(exitSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(exitSeconds), exitSeconds, "exitSeconds must not be negative and must be finite");
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
            Durability = durability ?? VehicleDurabilitySpec.Default;
            SpeedFieldOfView = speedFieldOfView ?? VehicleSpeedFieldOfViewSpec.Default;
            BailOut = bailOut ?? BailOutSpec.Default;
            EnterSeconds = enterSeconds;
            ExitSeconds = exitSeconds;
        }

        public string Id { get; }

        public int MaxHealth { get; }

        public IReadOnlyList<VehicleSeatSpec> Seats => _seats;

        public IReadOnlyList<VehicleWeaponSpec> Weapons => _weapons;

        public IReadOnlyList<VehiclePartSpec> Parts => _parts;

        public int DriverSeatIndex { get; }

        public float EnterRange { get; }

        public float ChaseDistance { get; }

        public float ChaseHeight { get; }

        public VehicleDurabilitySpec Durability { get; }

        public VehicleSpeedFieldOfViewSpec SpeedFieldOfView { get; }

        public BailOutSpec BailOut { get; }

        public float EnterSeconds { get; }

        public float ExitSeconds { get; }

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

        // 座席に乗り物の武器があればそちらを使い、自分の武器は出させない。両手で 2 つの武器を同時に撃たせないため。
        public VehicleSeatFiring FiringOf(int seatIndex)
        {
            if (WeaponsOfSeat(seatIndex).Count > 0)
            {
                return VehicleSeatFiring.VehicleWeapon;
            }

            return _seats[seatIndex].CanFireOwnWeapon ? VehicleSeatFiring.OwnWeapon : VehicleSeatFiring.None;
        }

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

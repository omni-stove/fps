using System;
using System.Collections.Generic;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class ClientVehicles
    {
        private readonly int _localPlayerId;
        private readonly Dictionary<int, Track> _tracks = new Dictionary<int, Track>();
        private readonly List<int> _gone = new List<int>();
        private readonly HashSet<int> _present = new HashSet<int>();
        private int _sequence;

        public ClientVehicles(int localPlayerId)
        {
            _localPlayerId = localPlayerId;
        }

        /// <summary>乗り物の壊れ方が変わった。初めて見た乗り物では、その時の段階で 1 度呼ぶ (途中から入った人に残骸を見せるため)。</summary>
        public event Action<int, VehicleDamageStage> StageChanged;

        public event Action<VehicleSeatSync> LocalSeatChanged;

        public event Action<int, Vector3, Quaternion, Vector3> Corrected;

        public VehicleSeatSync LocalSeat { get; private set; }

        public int CorrectionGeneration { get; private set; }

        public IEnumerable<int> Indices => _tracks.Keys;

        public void Apply(SnapshotMessage snapshot)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var vehicles = snapshot.Vehicles;
            _present.Clear();
            for (var i = 0; i < vehicles.Count; i++)
            {
                ref var row = ref vehicles[i];
                _present.Add(row.Index);
                var isNew = !_tracks.TryGetValue(row.Index, out var track);
                if (isNew)
                {
                    track = new Track();
                    _tracks.Add(row.Index, track);
                }

                if (track.History.Count == 0 || snapshot.Tick > track.History.NewestTick)
                {
                    track.History.Record(snapshot.Tick, row.Position, row.Rotation);
                }

                track.Velocity = row.Velocity;
                track.Health = row.Health;
                if (isNew || track.Stage != row.Stage)
                {
                    track.Stage = row.Stage;
                    StageChanged?.Invoke(row.Index, row.Stage);
                }
            }

            _gone.Clear();
            foreach (var index in _tracks.Keys)
            {
                if (!_present.Contains(index))
                {
                    _gone.Add(index);
                }
            }

            foreach (var index in _gone)
            {
                _tracks.Remove(index);
            }

            var seat = VehicleSeatSync.None;
            var own = snapshot.Players.IndexOf(_localPlayerId);
            if (own >= 0)
            {
                seat = snapshot.Players[own].Vehicle;
            }

            if (!seat.SameAs(LocalSeat))
            {
                LocalSeat = seat;
                LocalSeatChanged?.Invoke(seat);
            }
        }

        public bool TrySample(int vehicle, float renderTick, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.Identity;
            return _tracks.TryGetValue(vehicle, out var track) && track.History.TrySample(renderTick, out position, out rotation);
        }

        public bool TryGetState(int vehicle, out int health, out VehicleDamageStage stage)
        {
            if (_tracks.TryGetValue(vehicle, out var track))
            {
                health = track.Health;
                stage = track.Stage;
                return true;
            }

            health = 0;
            stage = VehicleDamageStage.Intact;
            return false;
        }

        public bool TryGetLatest(int vehicle, out Vector3 position, out Quaternion rotation, out Vector3 velocity)
        {
            velocity = default;
            if (!_tracks.TryGetValue(vehicle, out var track)
                || !track.History.TrySample(track.History.NewestTick, out position, out rotation))
            {
                position = default;
                rotation = Quaternion.Identity;
                return false;
            }

            velocity = track.Velocity;
            return true;
        }

        public bool FillMove(VehicleMoveMessage message, int driverSeat, Vector3 position, Quaternion rotation, Vector3 velocity)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (!LocalSeat.IsRiding || driverSeat < 0 || LocalSeat.Seat != driverSeat)
            {
                return false;
            }

            message.Vehicle = LocalSeat.Vehicle;
            message.Sequence = _sequence;
            message.CorrectionGeneration = CorrectionGeneration;
            message.Position = position;
            message.Rotation = rotation;
            message.Velocity = velocity;
            _sequence = _sequence == int.MaxValue ? 0 : _sequence + 1;
            return true;
        }

        public void Correct(VehicleCorrectedMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            CorrectionGeneration = message.Generation;
            Corrected?.Invoke(message.Vehicle, message.Position, message.Rotation, message.Velocity);
        }

        private sealed class Track
        {
            public VehiclePoseHistory History { get; } = new VehiclePoseHistory();

            public Vector3 Velocity { get; set; }

            public int Health { get; set; }

            public VehicleDamageStage Stage { get; set; }
        }
    }
}

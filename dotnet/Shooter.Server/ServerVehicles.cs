using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Numerics;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 動きを自分で計算せず運転席のクライアントの予測を確かめるだけにするのは、Unity 抜きのサーバーには車輪の物理が無いため。
    internal sealed partial class ServerVehicles : IServerModule, ISnapshotContributor
    {
        public const string LogTag = VehicleLog.Tag;

        // Unity の Vehicle.FallbackExitDistance と同じ。
        public const float FallbackExitDistance = 2f;

        // 降りる位置の当たりを確かめるとき、地面に触れただけで塞がっていることにしないよう体を浮かせる高さ。
        private const float _exitSkin = 0.05f;

        private const float _warningIntervalSeconds = 1f;

        private readonly MatchRulesModule _rules;
        private readonly ServerMovement _movement;
        private readonly List<Car> _cars = new List<Car>();
        private readonly SortedDictionary<int, Seat> _riders = new SortedDictionary<int, Seat>();
        private readonly List<int> _riderIds = new List<int>();
        private readonly VehicleCorrectedMessage _corrected = new VehicleCorrectedMessage();
        private readonly TeleportedMessage _teleported = new TeleportedMessage();
        private readonly int[] _overlap = new int[1];
        private ServerContext _context;
        private TextWriter _log;
        private ServerMatch _match;
        private ServerBodies _bodies;
        private long _tick;
        private float _now;
        private float _nextWarning = float.NegativeInfinity;

        public ServerVehicles(MatchRulesModule rules, ServerMovement movement)
        {
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            _movement = movement ?? throw new ArgumentNullException(nameof(movement));
        }

        public string Name => "vehicles";

        public int Count => _cars.Count;

        internal ServerCombat Combat { get; set; }

        public void Start(ServerContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _log = context.Log;
            _match = context.Match;
            _bodies = context.Bodies;
            UseMap(context.Map);

            _match.PlayerLeft += HandlePlayerLeft;
            _match.MapChanged += HandleMapChanged;
            _bodies.Teleported += HandleTeleported;
            _rules.MatchStarted += HandleMatchStarted;
            _log.WriteLine(LogTag + " net vehicles=" + Text(_cars.Count));
        }

        private void UseMap(ServerMapData map)
        {
            _cars.Clear();
            var vehicles = map.Vehicles.Vehicles;
            for (var i = 0; i < vehicles.Count; i++)
            {
                _cars.Add(new Car(i, vehicles[i]));
            }
        }

        // 乗り物の番号は新しいマップの並びを指すので、前のマップの乗り物から全員を降ろしてから替える。置き直しは続くルールの出現に任せる
        private void HandleMapChanged(ServerMapData map)
        {
            CopyRiders();
            foreach (var id in _riderIds)
            {
                Release(id, VehicleLeaveReason.Forced, false);
            }

            UseMap(map);
            _log.WriteLine(LogTag + " net vehicles=" + Text(_cars.Count) + " map=" + map.Name);
        }

        public void Tick(long tick, float deltaSeconds)
        {
            _tick = tick;
            _now += deltaSeconds;
            TickCountermeasures(deltaSeconds);
            foreach (var car in _cars)
            {
                car.Drive.Refill(deltaSeconds);
                if (!car.Exploded)
                {
                    var burn = car.Durability.TickBurn(deltaSeconds);
                    if (burn > 0)
                    {
                        Damage(car, burn, car.LastAttacker, false);
                    }
                }
            }

            CopyRiders();
            foreach (var id in _riderIds)
            {
                if (_riders.ContainsKey(id) && !CanRide(id))
                {
                    Release(id, VehicleLeaveReason.RiderDied, false);
                }
            }

            foreach (var pair in _riders)
            {
                PlaceRider(pair.Key, pair.Value);
            }

            var recorded = (int)Math.Min(tick, int.MaxValue);
            foreach (var car in _cars)
            {
                car.History.Record(recorded, car.Position, car.Rotation);
            }
        }

        public void OnConnected(ConnectionId connection)
        {
        }

        public void OnDisconnected(ConnectionId connection)
        {
        }

        public void OnMessage(ConnectionId connection, IMessage message)
        {
            if (!_match.TryGetPlayer(connection, out var player))
            {
                return;
            }

            switch (message)
            {
                case VehicleRequestMessage request:
                    Handle(player.PlayerId, request.Action, request.Vehicle, request.Seat);
                    break;
                case VehicleMoveMessage move:
                    Move(player.PlayerId, move);
                    break;
            }
        }

        public void Stop()
        {
            if (_match == null)
            {
                return;
            }

            _match.PlayerLeft -= HandlePlayerLeft;
            _match.MapChanged -= HandleMapChanged;
            _bodies.Teleported -= HandleTeleported;
            _rules.MatchStarted -= HandleMatchStarted;
            _riders.Clear();
            _cars.Clear();
        }

        public bool Handle(int playerId, VehicleAction action, int vehicle, int seat)
        {
            switch (action)
            {
                case VehicleAction.Board:
                    return Board(playerId, vehicle, seat);
                case VehicleAction.Leave:
                    return Leave(playerId);
                case VehicleAction.SwitchSeat:
                    return Switch(playerId, seat);
                case VehicleAction.Countermeasure:
                    return DeployCountermeasure(playerId);
                default:
                    return false;
            }
        }

        public void Contribute(SnapshotMessage snapshot, ServerPlayer receiver)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            foreach (var car in _cars)
            {
                if (snapshot.Vehicles.Count == snapshot.Vehicles.MaxCount)
                {
                    break;
                }

                ref var row = ref snapshot.Vehicles.Add();
                row.Index = car.Index;
                row.Position = car.Position;
                row.Rotation = car.Rotation;
                row.Velocity = car.Velocity;
                row.Health = car.Health;
                row.Stage = car.Stage;
                row.Parts = car.Parts;
            }

            var players = snapshot.Players;
            for (var i = 0; i < players.Count; i++)
            {
                ref var row = ref players[i];
                row.Vehicle = _riders.TryGetValue(row.PlayerId, out var seat) ? new VehicleSeatSync(seat.Car.Index, seat.Index) : VehicleSeatSync.None;
            }
        }

        public bool TryGetSeat(int playerId, out int vehicle, out int seat)
        {
            if (_riders.TryGetValue(playerId, out var riding))
            {
                vehicle = riding.Car.Index;
                seat = riding.Index;
                return true;
            }

            vehicle = -1;
            seat = -1;
            return false;
        }

        public bool TryGetState(int vehicle, out Vector3 position, out Quaternion rotation, out int health, out VehicleDamageStage stage)
        {
            if (vehicle < 0 || vehicle >= _cars.Count)
            {
                position = default;
                rotation = Quaternion.Identity;
                health = 0;
                stage = VehicleDamageStage.Intact;
                return false;
            }

            var car = _cars[vehicle];
            position = car.Position;
            rotation = car.Rotation;
            health = car.Health;
            stage = car.Stage;
            return true;
        }

        public MapVehicle MapOf(int vehicle)
        {
            return _cars[vehicle].Map;
        }

        public int OccupantOf(int vehicle, int seat)
        {
            return _cars[vehicle].Occupants[seat];
        }

        private bool Board(int playerId, int vehicle, int requestedSeat)
        {
            var rider = Text(playerId);
            if (vehicle < 0 || vehicle >= _cars.Count)
            {
                _log.WriteLine(VehicleLog.BoardRefused("#" + Text(vehicle), rider, VehicleLog.ReasonUnknownVehicle));
                return false;
            }

            var car = _cars[vehicle];
            var refusal = _riders.ContainsKey(playerId) ? VehicleLog.ReasonAlreadyRiding
                : car.Exploded ? VehicleLog.ReasonWrecked
                : !CanRide(playerId) || !_bodies.TryGet(playerId, out _) ? VehicleLog.ReasonRiderUnavailable
                : null;
            if (refusal != null)
            {
                _log.WriteLine(VehicleLog.BoardRefused(car.Name, rider, refusal));
                return false;
            }

            _bodies.TryGet(playerId, out var body);
            var feet = body.Position + new Vector3(0f, body.Shape.BottomY, 0f);
            var seat = ChooseSeat(car, requestedSeat, feet, out refusal);
            if (seat < 0)
            {
                _log.WriteLine(VehicleLog.BoardRefused(car.Name, rider, refusal));
                return false;
            }

            car.Occupants[seat] = playerId;
            var riding = new Seat(car, seat);
            _riders.Add(playerId, riding);
            PlaceRider(playerId, riding);
            var role = car.Map.Spec.Seats[seat].Role;
            _log.WriteLine(VehicleLog.Board(car.Name, rider, seat, role));
            _match.WriteVehicleEntered(playerId, car.Index, car.Map.Spec.Id, seat, role);
            if (seat == car.Map.Spec.DriverSeatIndex)
            {
                HandOver(car, playerId);
            }

            return true;
        }

        private static int ChooseSeat(Car car, int requested, Vector3 feet, out string refusal)
        {
            var map = car.Map;
            var range = map.Spec.EnterRange;
            if (requested != VehicleRequestMessage.AnySeat)
            {
                refusal = requested >= car.Occupants.Length ? VehicleLog.ReasonSeatRemoved
                    : car.Occupants[requested] != PlayerIds.None ? VehicleLog.ReasonSeatTaken
                    : map.DistanceToEnter(requested, car.Position, car.Rotation, feet) > range ? VehicleLog.ReasonOutOfRange
                    : null;
                return refusal == null ? requested : -1;
            }

            var best = -1;
            var bestDistance = float.PositiveInfinity;
            var anyFree = false;
            for (var i = 0; i < car.Occupants.Length; i++)
            {
                if (car.Occupants[i] != PlayerIds.None)
                {
                    continue;
                }

                anyFree = true;
                var distance = map.DistanceToEnter(i, car.Position, car.Rotation, feet);
                if (distance <= range && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }

            refusal = best >= 0 ? null : anyFree ? VehicleLog.ReasonOutOfRange : VehicleLog.ReasonNoFreeSeat;
            return best;
        }

        private bool Leave(int playerId)
        {
            if (!_riders.TryGetValue(playerId, out var riding))
            {
                _log.WriteLine(VehicleLog.LeaveRefused("-", Text(playerId), VehicleLog.ReasonNotRiding));
                return false;
            }

            if (!TryFindExit(riding, playerId, false, out _, out _))
            {
                _log.WriteLine(VehicleLog.LeaveRefused(riding.Car.Name, Text(playerId), VehicleLog.ReasonExitBlocked));
                return false;
            }

            Release(playerId, VehicleLeaveReason.Request, true);
            return true;
        }

        private bool Switch(int playerId, int requested)
        {
            var rider = Text(playerId);
            if (!_riders.TryGetValue(playerId, out var riding))
            {
                _log.WriteLine(VehicleLog.SwitchRefused("-", rider, VehicleLog.ReasonNotRiding));
                return false;
            }

            var car = riding.Car;
            var occupants = car.Occupants;
            var target = -1;
            string refusal = null;
            if (requested == VehicleRequestMessage.AnySeat)
            {
                for (var step = 1; step < occupants.Length && target < 0; step++)
                {
                    var candidate = (riding.Index + step) % occupants.Length;
                    if (occupants[candidate] == PlayerIds.None)
                    {
                        target = candidate;
                    }
                }

                refusal = target < 0 ? VehicleLog.ReasonNoFreeSeat : null;
            }
            else
            {
                refusal = requested >= occupants.Length ? VehicleLog.ReasonSeatRemoved
                    : requested == riding.Index || occupants[requested] != PlayerIds.None ? VehicleLog.ReasonSeatTaken
                    : null;
                target = refusal == null ? requested : -1;
            }

            if (refusal != null)
            {
                _log.WriteLine(VehicleLog.SwitchRefused(car.Name, rider, refusal));
                return false;
            }

            occupants[riding.Index] = PlayerIds.None;
            occupants[target] = playerId;
            var moved = new Seat(car, target);
            _riders[playerId] = moved;
            PlaceRider(playerId, moved);
            _log.WriteLine(VehicleLog.Switch(car.Name, rider, riding.Index, target, car.Map.Spec.Seats[target].Role));
            if (riding.Index == car.Map.Spec.DriverSeatIndex)
            {
                car.Velocity = Vector3.Zero;
            }

            if (target == car.Map.Spec.DriverSeatIndex)
            {
                HandOver(car, playerId);
            }

            return true;
        }

        // 座席から外し、teleport なら降りる位置へ置き直す。倒れた・抜けた人は置き直す先が要らない (出現で置き直される) ので外すだけにする。
        private void Release(int playerId, VehicleLeaveReason reason, bool teleport)
        {
            if (!_riders.TryGetValue(playerId, out var riding))
            {
                return;
            }

            var exit = -1;
            var origin = default(Vector3);
            if (teleport)
            {
                TryFindExit(riding, playerId, true, out origin, out exit);
            }

            var car = riding.Car;
            _riders.Remove(playerId);
            car.Occupants[riding.Index] = PlayerIds.None;
            if (riding.Index == car.Map.Spec.DriverSeatIndex)
            {
                car.Velocity = Vector3.Zero;
            }

            _movement.Dismount(playerId);
            _log.WriteLine(VehicleLog.Leave(car.Name, Text(playerId), riding.Index, reason, exit));
            _match.WriteVehicleLeft(playerId, car.Index, car.Map.Spec.Id, riding.Index, reason);
            if (teleport && _bodies.TryGet(playerId, out var body))
            {
                _bodies.Teleport(playerId, origin, body.Yaw, _tick);
                _teleported.PlayerId = playerId;
                _teleported.Tick = (int)Math.Min(_tick, int.MaxValue);
                _teleported.Position = origin;
                _teleported.Yaw = body.Yaw;
                foreach (var player in _match.Players)
                {
                    _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _teleported);
                }
            }
        }

        // 塞がっていない最初の降りる位置を選ぶ。force なら全部塞がっていても最初の候補を返し、exit を -1 にする。
        private bool TryFindExit(Seat riding, int playerId, bool force, out Vector3 origin, out int exit)
        {
            var car = riding.Car;
            var map = car.Map;
            var candidates = map.ExitsOf(riding.Index);
            var geometry = _bodies.Geometry;
            var shape = geometry.Shape(false);
            geometry.SphereCenters(shape, out var bottomY, out var topY);
            var count = Math.Max(1, candidates.Count);
            var first = default(Vector3);
            for (var i = 0; i < count; i++)
            {
                var local = candidates.Count > 0 ? candidates[i] : FallbackExit(map.Seats[riding.Index].Sit);
                var feet = MapVehicle.ToWorld(local, car.Position, car.Rotation);
                var candidate = MapVehicle.RiderOrigin(feet, geometry, false);
                if (i == 0)
                {
                    first = candidate;
                }

                var lift = new Vector3(0f, _exitSkin, 0f);
                var bottom = candidate + lift + new Vector3(0f, bottomY, 0f);
                var top = candidate + lift + new Vector3(0f, topY, 0f);
                if (_context.World.OverlapCapsule(bottom, top, geometry.Radius, ServerHitboxes.RaycastLayers, _overlap) == 0)
                {
                    origin = candidate;
                    exit = i;
                    return true;
                }
            }

            origin = first;
            exit = -1;
            return force;
        }

        private static Vector3 FallbackExit(Vector3 sit)
        {
            var side = sit.X < 0f ? -1f : 1f;
            return new Vector3(sit.X + (side * FallbackExitDistance), sit.Y, sit.Z);
        }

        private void PlaceRider(int playerId, Seat riding)
        {
            var car = riding.Car;
            var crouched = car.Map.Spec.Seats[riding.Index].CrouchesRider;
            var feet = car.Map.SeatFeet(riding.Index, car.Position, car.Rotation);
            _movement.Ride(playerId, MapVehicle.RiderOrigin(feet, _bodies.Geometry, crouched), car.Velocity, crouched);
        }

        private bool CanRide(int playerId)
        {
            var session = _rules.Session;
            return session != null && session.HasPlayer(playerId) && session.IsAlive(playerId) && !session.IsDowned(playerId);
        }

        private void HandlePlayerLeft(ServerPlayer player)
        {
            Release(player.PlayerId, VehicleLeaveReason.RiderRemoved, false);
        }

        // 降ろすときの置き直しは座席から外した後に行うので、ここに来るのはルールが出現させたときなど、乗り物の外から置き直されたときだけ。
        private void HandleTeleported(ServerBody body)
        {
            Release(body.PlayerId, VehicleLeaveReason.Forced, false);
        }

        // 次の試合は壊れていない乗り物で始める。建物と違い、置き場所に戻さないと残骸と運転した先のままになるため。
        private void HandleMatchStarted()
        {
            CopyRiders();
            foreach (var id in _riderIds)
            {
                Release(id, VehicleLeaveReason.Forced, true);
            }

            foreach (var car in _cars)
            {
                car.Reset();
            }
        }

        private void CopyRiders()
        {
            _riderIds.Clear();
            _riderIds.AddRange(_riders.Keys);
        }

        private bool CanWarn()
        {
            if (_now < _nextWarning)
            {
                return false;
            }

            _nextWarning = _now + _warningIntervalSeconds;
            return true;
        }

        private static string Text(int value) => value.ToString(CultureInfo.InvariantCulture);

        private readonly struct Seat
        {
            public Seat(Car car, int index)
            {
                Car = car;
                Index = index;
            }

            public Car Car { get; }

            public int Index { get; }
        }

        private sealed class Car
        {
            public Car(int index, MapVehicle map)
            {
                Index = index;
                Map = map;
                Name = VehicleLog.NetName(map.Spec.Id, index);
                Occupants = new int[map.Seats.Count];
                Drive = new VehicleDriveCheck(map.Drive);
                Reset();
            }

            public int Index { get; }

            public MapVehicle Map { get; }

            public string Name { get; }

            public int[] Occupants { get; }

            public VehicleDurability Durability { get; private set; }

            public VehicleDriveCheck Drive { get; }

            public VehiclePoseHistory History { get; } = new VehiclePoseHistory(LagCompensator.DefaultMaxRewindTicks + 2);

            public Vector3 Position { get; set; }

            public Quaternion Rotation { get; set; }

            public Vector3 Velocity { get; set; }

            public int Health { get; set; }

            public bool Exploded { get; set; }

            public int? LastAttacker { get; set; }

            public VehiclePartDamageSync Parts { get; set; }

            // フレアを積んでいない乗り物は null。
            public CountermeasurePod Flares { get; private set; }

            public int LastSequence { get; set; }

            public int Generation { get; set; }

            public VehicleDamageStage Stage => Exploded ? VehicleDamageStage.Destroyed : Durability.Stage;

            public void Reset()
            {
                Array.Fill(Occupants, PlayerIds.None);
                Position = Map.Position;
                Rotation = Map.Rotation;
                Velocity = Vector3.Zero;
                Health = Map.Spec.MaxHealth;
                Exploded = false;
                LastAttacker = null;
                Parts = VehiclePartDamageSync.None;
                Flares = Map.Countermeasures == null ? null : new CountermeasurePod(Map.Countermeasures.Spec);
                LastSequence = -1;
                Durability = new VehicleDurability(Map.Spec.Durability);
                Drive.Reset();
                History.Clear();
            }
        }
    }
}

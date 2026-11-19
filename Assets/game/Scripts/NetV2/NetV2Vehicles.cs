using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;
using Shooter.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Shooter.NetV2
{
    // 乗り降り・耐久・爆発・部位への命中はサーバーが決めるので、シーンの乗り物の部品 (Vehicle・VehicleImpactDamage・部位の部品 INetVehicleParts の命中の受け口) は止め、
    // 煙・火・残骸の見た目 (VehicleDamageEffects) にサーバーの段階を、部位の部品 (INetVehicleParts) にサーバーの受けた量を渡す。
    // 乗り物の番号は書き出し (MapVehicleExporter) と同じく、有効な乗り物をシーンの並び順で数える。
    internal sealed class NetV2Vehicles
    {
        private readonly List<SceneVehicle> _scene = new List<SceneVehicle>();
        private readonly VehicleRequestMessage _request = new VehicleRequestMessage();
        private readonly VehicleMoveMessage _move = new VehicleMoveMessage();
        private readonly UnityEngine.Object _context;
        private ClientVehicles _vehicles;
        private StanceGeometry _geometry;
        private int _driving = VehicleSeatSync.NoVehicle;

        public NetV2Vehicles(UnityEngine.Object context)
        {
            _context = context;
        }

        public int Count => _scene.Count;

        public void Enter(Scene scene, ClientVehicles vehicles, StanceGeometry geometry, NetV2Weapons weapons)
        {
            Leave();
            _vehicles = vehicles ?? throw new ArgumentNullException(nameof(vehicles));
            _geometry = geometry ?? throw new ArgumentNullException(nameof(geometry));
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var vehicle in root.GetComponentsInChildren<Vehicle>(false))
                {
                    if (vehicle.enabled && vehicle.gameObject.activeInHierarchy)
                    {
                        _scene.Add(Take(vehicle));
                    }
                }
            }

            if (weapons != null)
            {
                weapons.SceneDefinitions = FindWeapon;
            }

            _vehicles.StageChanged += HandleStageChanged;
            _vehicles.PartsChanged += HandlePartsChanged;
            _vehicles.LocalSeatChanged += HandleSeatChanged;
            _vehicles.Corrected += HandleCorrected;
            Debug.Log(NetV2Client.LogTag + " vehicles in scene " + scene.name + ": " + _scene.Count.ToString(CultureInfo.InvariantCulture), _context);
            foreach (var index in _vehicles.Indices)
            {
                if (_vehicles.TryGetState(index, out _, out var stage))
                {
                    HandleStageChanged(index, stage);
                }

                if (_vehicles.TryGetParts(index, out var parts))
                {
                    HandlePartsChanged(index, parts);
                }
            }

            HandleSeatChanged(_vehicles.LocalSeat);
        }

        public void Leave()
        {
            if (_vehicles != null)
            {
                _vehicles.StageChanged -= HandleStageChanged;
                _vehicles.PartsChanged -= HandlePartsChanged;
                _vehicles.LocalSeatChanged -= HandleSeatChanged;
                _vehicles.Corrected -= HandleCorrected;
            }

            _vehicles = null;
            _driving = VehicleSeatSync.NoVehicle;
            _scene.Clear();
        }

        public void Show(float renderTick, NetV2LocalPlayer local)
        {
            if (_vehicles == null)
            {
                return;
            }

            for (var i = 0; i < _scene.Count; i++)
            {
                var scene = _scene[i];
                if (scene.Vehicle == null || i == _driving)
                {
                    continue;
                }

                if (_vehicles.TrySample(i, renderTick, out var position, out var rotation))
                {
                    scene.Vehicle.transform.SetPositionAndRotation(NetV2Math.ToUnity(position), NetV2Math.ToUnity(rotation));
                }
            }

            if (_driving >= 0 && local != null && _driving < _scene.Count && _scene[_driving].Motor != null && _scene[_driving].Vehicle != null)
            {
                local.ReadDrive(out var move, out var boost, out var vertical);
                var vehicle = _scene[_driving].Vehicle;
                var view = local.Camera != null ? local.Camera.transform : local.transform;
                var input = new VehicleSeatInput(move, vertical, boost, local.IsAttackHeld, local.IsAiming, false, view.position, view.forward);
                var seats = vehicle.Seats;
                var driver = vehicle.Spec.DriverSeatIndex;
                var seat = driver >= 0 && driver < seats.Count ? seats[driver] : null;
                _scene[_driving].Motor.SetInput(VehicleControlSchemes.For(vehicle.gameObject).ToMotorInput(seat, input, Time.deltaTime));
            }
        }

        public bool TrySeatOrigin(out Vector3 origin, out bool crouched)
        {
            origin = default;
            crouched = false;
            var seat = _vehicles != null ? _vehicles.LocalSeat : VehicleSeatSync.None;
            if (!seat.IsRiding || seat.Vehicle >= _scene.Count || _scene[seat.Vehicle].Vehicle == null)
            {
                return false;
            }

            var seats = _scene[seat.Vehicle].Vehicle.Seats;
            if (seat.Seat >= seats.Count)
            {
                return false;
            }

            crouched = seats[seat.Seat].Spec.CrouchesRider;
            var feet = NetV2Math.ToNumerics(seats[seat.Seat].SitPosition);
            origin = NetV2Math.ToUnity(MapVehicle.RiderOrigin(feet, _geometry, crouched));
            return true;
        }

        // オフラインの PlayerVehicleRider と同じく、自分の武器を撃てる席 (VehicleSeatFiring.OwnWeapon) でだけ一人称の武器を残す。
        // シーンに無い乗り物の席は撃てる席か分からないので、武器を構えた手が乗り物と重ならないよう隠す側に倒す。
        public bool HidesOwnWeapon()
        {
            var seat = _vehicles != null ? _vehicles.LocalSeat : VehicleSeatSync.None;
            if (!seat.IsRiding)
            {
                return false;
            }

            var vehicle = seat.Vehicle < _scene.Count ? _scene[seat.Vehicle].Vehicle : null;
            if (vehicle == null || seat.Seat < 0 || seat.Seat >= vehicle.Seats.Count)
            {
                return true;
            }

            return vehicle.FiringOf(vehicle.Seats[seat.Seat]) != VehicleSeatFiring.OwnWeapon;
        }

        public void SendRequests(NetV2LocalPlayer local, MatchClient client, Vector3 feet)
        {
            if (_vehicles == null || local == null || client == null)
            {
                return;
            }

            var seat = _vehicles.LocalSeat;
            if (local.TakeInteract())
            {
                if (seat.IsRiding)
                {
                    Request(client, VehicleAction.Leave, seat.Vehicle, VehicleRequestMessage.AnySeat);
                }
                else if (TryFindNearest(feet, out var nearest))
                {
                    Request(client, VehicleAction.Board, nearest, VehicleRequestMessage.AnySeat);
                }
            }

            var next = local.TakeNextSeat();
            var previous = local.TakePreviousSeat();
            if (!seat.IsRiding || seat.Vehicle >= _scene.Count)
            {
                return;
            }

            if (next)
            {
                Request(client, VehicleAction.SwitchSeat, seat.Vehicle, VehicleRequestMessage.AnySeat);
            }
            else if (previous)
            {
                var count = _scene[seat.Vehicle].Vehicle.Seats.Count;
                Request(client, VehicleAction.SwitchSeat, seat.Vehicle, (seat.Seat + count - 1) % count);
            }
        }

        public void SendMove(MatchClient client)
        {
            if (_vehicles == null || client == null || _driving < 0 || _driving >= _scene.Count)
            {
                return;
            }

            var scene = _scene[_driving];
            if (scene.Vehicle == null)
            {
                return;
            }

            var transform = scene.Vehicle.transform;
            var velocity = scene.Body != null ? scene.Body.linearVelocity : Vector3.zero;
            if (_vehicles.FillMove(
                    _move,
                    scene.Vehicle.Spec.DriverSeatIndex,
                    NetV2Math.ToNumerics(transform.position),
                    NetV2Math.ToNumerics(transform.rotation),
                    NetV2Math.ToNumerics(velocity)))
            {
                client.Send(_move, TransportChannel.Unreliable);
            }
        }

        private static SceneVehicle Take(Vehicle vehicle)
        {
            var body = vehicle.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
            }

            var impact = vehicle.GetComponent<VehicleImpactDamage>();
            if (impact != null)
            {
                impact.enabled = false;
            }

            var parts = vehicle.GetComponentInChildren<INetVehicleParts>(true);
            if (parts != null)
            {
                parts.Detach();
            }

            var motor = vehicle.GetComponent<IVehicleMotor>();
            var effects = vehicle.GetComponentInChildren<VehicleDamageEffects>(true);
            var mounts = vehicle.WeaponMounts;
            vehicle.enabled = false;
            return new SceneVehicle(vehicle, body, motor, effects, parts, mounts);
        }

        private bool TryFindNearest(Vector3 feet, out int nearest)
        {
            nearest = -1;
            var best = float.PositiveInfinity;
            for (var i = 0; i < _scene.Count; i++)
            {
                var vehicle = _scene[i].Vehicle;
                if (vehicle == null)
                {
                    continue;
                }

                var range = vehicle.Spec.EnterRange;
                foreach (var seat in vehicle.Seats)
                {
                    var distance = seat.DistanceToEnter(feet);
                    if (distance <= range && distance < best)
                    {
                        best = distance;
                        nearest = i;
                    }
                }
            }

            return nearest >= 0;
        }

        private void Request(MatchClient client, VehicleAction action, int vehicle, int seat)
        {
            _request.Action = action;
            _request.Vehicle = vehicle;
            _request.Seat = seat;
            client.Send(_request, TransportChannel.Reliable);
            Debug.Log(VehicleLog.Tag + " net request action=" + action + " vehicle=" + vehicle.ToString(CultureInfo.InvariantCulture)
                + " seat=" + seat.ToString(CultureInfo.InvariantCulture), _context);
        }

        private WeaponDefinition FindWeapon(string weaponId)
        {
            foreach (var scene in _scene)
            {
                foreach (var mount in scene.Mounts)
                {
                    if (mount != null && mount.HasWeapon && string.Equals(mount.WeaponSpec.Id, weaponId, StringComparison.Ordinal))
                    {
                        return mount.Definition;
                    }
                }
            }

            return null;
        }

        private void HandleStageChanged(int index, VehicleDamageStage stage)
        {
            if (index < _scene.Count && _scene[index].Effects != null)
            {
                _scene[index].Effects.Show(stage);
            }
        }

        private void HandlePartsChanged(int index, VehiclePartDamageSync parts)
        {
            if (index >= _scene.Count || _scene[index].Parts == null || _scene[index].Vehicle == null)
            {
                return;
            }

            _scene[index].Parts.ApplyServerTaken(parts.TakenOf);
        }

        // 運転席に着いたら自分の物理で動かし、離れたら補間の姿を置けるよう物理を止める
        private void HandleSeatChanged(VehicleSeatSync seat)
        {
            var driving = VehicleSeatSync.NoVehicle;
            if (seat.IsRiding && seat.Vehicle < _scene.Count && _scene[seat.Vehicle].Vehicle != null
                && seat.Seat == _scene[seat.Vehicle].Vehicle.Spec.DriverSeatIndex)
            {
                driving = seat.Vehicle;
            }

            if (driving == _driving)
            {
                return;
            }

            if (_driving >= 0 && _driving < _scene.Count)
            {
                var previous = _scene[_driving];
                previous.Motor?.SetInput(VehicleMotorInput.Idle);
                if (previous.Vehicle != null && VehicleControlSchemes.For(previous.Vehicle.gameObject) is IVehicleControlSchemeReset reset)
                {
                    reset.ResetState();
                }

                if (previous.Body != null)
                {
                    previous.Body.isKinematic = true;
                }
            }

            _driving = driving;
            if (driving < 0)
            {
                return;
            }

            var scene = _scene[driving];
            if (_vehicles.TryGetLatest(driving, out var position, out var rotation, out var velocity))
            {
                Place(scene, position, rotation, velocity);
            }

            if (scene.Body != null)
            {
                scene.Body.isKinematic = false;
            }
        }

        private void HandleCorrected(int index, System.Numerics.Vector3 position, System.Numerics.Quaternion rotation, System.Numerics.Vector3 velocity)
        {
            if (index >= _scene.Count || _scene[index].Vehicle == null)
            {
                return;
            }

            Debug.Log(VehicleLog.Tag + " net corrected vehicle=" + index.ToString(CultureInfo.InvariantCulture)
                + " at=" + NetV2Math.ToUnity(position).ToString("F2", CultureInfo.InvariantCulture), _context);
            Place(_scene[index], position, rotation, velocity);
        }

        private static void Place(SceneVehicle scene, System.Numerics.Vector3 position, System.Numerics.Quaternion rotation, System.Numerics.Vector3 velocity)
        {
            scene.Vehicle.transform.SetPositionAndRotation(NetV2Math.ToUnity(position), NetV2Math.ToUnity(rotation));
            if (scene.Body != null)
            {
                scene.Body.position = NetV2Math.ToUnity(position);
                scene.Body.rotation = NetV2Math.ToUnity(rotation);
                if (!scene.Body.isKinematic)
                {
                    scene.Body.linearVelocity = NetV2Math.ToUnity(velocity);
                    scene.Body.angularVelocity = Vector3.zero;
                }
            }
        }

        private sealed class SceneVehicle
        {
            public SceneVehicle(
                Vehicle vehicle, Rigidbody body, IVehicleMotor motor, VehicleDamageEffects effects, INetVehicleParts parts,
                IReadOnlyList<VehicleWeaponMount> mounts)
            {
                Vehicle = vehicle;
                Body = body;
                Motor = motor;
                Effects = effects;
                Parts = parts;
                Mounts = mounts ?? Array.Empty<VehicleWeaponMount>();
            }

            public Vehicle Vehicle { get; }

            public Rigidbody Body { get; }

            public IVehicleMotor Motor { get; }

            public VehicleDamageEffects Effects { get; }

            public INetVehicleParts Parts { get; }

            public IReadOnlyList<VehicleWeaponMount> Mounts { get; }
        }
    }
}

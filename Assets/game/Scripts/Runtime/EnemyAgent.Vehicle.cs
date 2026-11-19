using System;
using System.Collections.Generic;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    // 敵とモードの Bot は入力を持たないので、乗り降りはコードから行う。
    public sealed partial class EnemyAgent : IVehicleRider
    {
        private readonly RiderVisibility _vehicleHidden = new RiderVisibility();
        private readonly List<Transform> _wheelPoints = new List<Transform>();
        private VehicleSeat _vehicleSeat;

        // 運転の部品を運転席に座っている間だけ付けるのは、降りた後も残すと、飛行機の舵を握ったままになり、借りた部品も元の持ち主へ戻らないため。
        private Vehicle _wheelVehicle;
        private BotVehicleDriver _wheelDriver;
        private IBotPilot _wheelPilot;
        private VehicleRoute _wheelRoute;
        private bool _ownsWheel;
        private Vehicle _wheelPreviousVehicle;
        private IVehicleRider _wheelPreviousRider;
        private VehicleRoute _wheelPreviousRoute;
        private bool _wheelPreviousUnmanned;
        private bool _wheelPreviousEnabled;
        private static readonly HashSet<Component> PendingWheelParts = new HashSet<Component>();
        private Transform[] _wheelPreviousWaypoints = Array.Empty<Transform>();

        public VehicleSeat Seat => _vehicleSeat;

        public bool IsRiding => _vehicleSeat != null;

        public Vehicle Vehicle => _vehicleSeat != null ? _vehicleSeat.Vehicle : null;

        string IVehicleRider.RiderName => Id;

        Transform IVehicleRider.RiderRoot => transform;

        float IVehicleRider.FeetBelowRoot => _agent != null ? _agent.baseOffset : 0f;

        float IVehicleRider.ClearanceRadius => _agent != null ? _agent.radius : 0.5f;

        float IVehicleRider.ClearanceHeight => _agent != null ? _agent.height : 2f;

        bool IVehicleRider.CanBoard => enabled && gameObject.activeInHierarchy && _brain != null && !_brain.IsDead;

        public bool TryBoard(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            return vehicle.TryEnter(this);
        }

        public bool TryBoard(Vehicle vehicle, int seatIndex, bool requireInRange = true)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            return vehicle.TryEnter(this, seatIndex, requireInRange);
        }

        public bool TryLeaveVehicle()
        {
            var vehicle = Vehicle;
            return vehicle != null && vehicle.TryExit(this);
        }

        public bool TrySwitchVehicleSeat(int seatIndex)
        {
            var vehicle = Vehicle;
            return vehicle != null && vehicle.TrySwitchToSeat(this, seatIndex);
        }

        public BotVehicleDriver VehicleDriver => _wheelDriver;

        public IBotPilot VehiclePilot => IsLive(_wheelPilot) ? _wheelPilot : null;

        public bool IsDrivingVehicle => _wheelDriver != null || IsLive(_wheelPilot);

        // 操縦の部品が要るかを動き方の部品に聞くのは、操縦の部品が舵を決めるのにその動き方を読むので、どの部品で操縦するかを動き方が知っているため。
        public static bool NeedsBotPilot(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            return vehicle.TryGetComponent<IBotPilotedMotor>(out _);
        }

        // AddComponent に渡す前に確かめるのは、Component でない型は例外になり、IBotPilot でない部品は付けても舵を握らずに乗り物へ残るため。
        public static bool IsBotPilotType(Type type)
        {
            return type != null
                && !type.IsAbstract
                && typeof(MonoBehaviour).IsAssignableFrom(type)
                && typeof(IBotPilot).IsAssignableFrom(type);
        }

        // 乗り物に付いた道より指示を先にするのは、同じ乗り物でも乗る敵ごとに別の道を走らせられるようにするため。
        public static VehicleRoute ChooseDriveRoute(VehicleRoute ordered, Vehicle vehicle)
        {
            if (ordered != null)
            {
                return ordered;
            }

            var onVehicle = vehicle != null ? vehicle.GetComponent<VehicleRoute>() : null;

            // 点が空の道は直下の子を点にするが、乗り物の子は座席など乗り物と一緒に動くので、着かない道になる。
            return onVehicle != null && onVehicle.Points.Count > 0 ? onVehicle : null;
        }

        void IVehicleRider.OnBoarded(VehicleSeat seat)
        {
            _vehicleSeat = seat ?? throw new ArgumentNullException(nameof(seat));
            StopForVehicle();
            ApplySeatPose(seat);
            FollowSeat();
            TakeWheel(seat);
        }

        void IVehicleRider.OnSeatChanged(VehicleSeat from, VehicleSeat to)
        {
            _vehicleSeat = to ?? throw new ArgumentNullException(nameof(to));
            ApplySeatPose(to);
            FollowSeat();
            TakeWheel(to);
        }

        void IVehicleRider.OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
        {
            _vehicleSeat = null;
            GiveUpWheel();

            // 乗っている間に受けた吹き飛ばしと怯みは進まず残るので、降りるときに捨てる。
            _flight.Clear();
            _interruption.Clear();
            _vehicleHidden.Restore();
            SetCrouching(false);
            var baseOffset = _agent != null ? _agent.baseOffset : 0f;
            transform.SetPositionAndRotation(feetPosition + (Vector3.up * baseOffset), rotation);
            _lastFinitePosition = transform.position;

            // 倒れて降ろされたときは、倒れたときに止めた NavMeshAgent をそのままにする。
            if (_agent == null || _brain == null || _brain.IsDead)
            {
                return;
            }

            _agent.enabled = true;
            if (_agent.isOnNavMesh)
            {
                _agent.Warp(feetPosition);
                _agent.isStopped = false;
                _warnedOffMesh = false;
            }
        }

        private bool HoldWhileRiding()
        {
            if (ReferenceEquals(_vehicleSeat, null))
            {
                return false;
            }

            if (_vehicleSeat == null || _vehicleSeat.Vehicle == null)
            {
                // 座席ごと消えて降ろしてもらえなかったときは、その場で降りたことにする。
                ((IVehicleRider)this).OnLeft(null, VehicleLeaveReason.RiderRemoved, GroundPosition, transform.rotation);
                return false;
            }

            FollowSeat();
            return true;
        }

        // 乗ったまま消えると機体に付けた操縦の部品が、いない乗り手のために舵を握ったまま残るので返す。
        private void GiveUpWheelOnDestroy()
        {
            GiveUpWheel(destroying: true);
        }

        private void LeaveVehicleOnDeath()
        {
            var vehicle = Vehicle;
            if (vehicle != null)
            {
                vehicle.ForceExit(this, VehicleLeaveReason.RiderDied);
            }
        }

        private void LeaveVehicleForReset()
        {
            var vehicle = Vehicle;
            if (vehicle != null)
            {
                vehicle.ForceExit(this, VehicleLeaveReason.Forced);
            }
        }

        private void StopForVehicle()
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.ResetPath();
            }

            _destination = null;
            _detour = null;
            _goal = null;
            _stuck?.Reset();
            _sprinting = false;
            _aiming = false;
            if (_agent != null)
            {
                _agent.enabled = false;
            }
        }

        private void ApplySeatPose(VehicleSeat seat)
        {
            SetCrouching(seat.Spec.CrouchesRider);
            _vehicleHidden.Restore();
            if (seat.Pose == VehicleSeatPose.Hidden)
            {
                _vehicleHidden.Hide(transform);
            }
        }

        private void FollowSeat()
        {
            var seat = _vehicleSeat;
            var baseOffset = _agent != null ? _agent.baseOffset : 0f;
            transform.SetPositionAndRotation(
                seat.SitPosition + (Vector3.up * baseOffset),
                Quaternion.Euler(0f, seat.SitYawDegrees, 0f));
            _lastFinitePosition = transform.position;
        }

        private void TakeWheel(VehicleSeat seat)
        {
            var vehicle = seat.Vehicle;
            if (seat.Role != VehicleSeatRole.Driver || vehicle == null)
            {
                GiveUpWheel();
                return;
            }

            if (IsDrivingVehicle && ReferenceEquals(_wheelVehicle, vehicle))
            {
                return;
            }

            GiveUpWheel();

            // 運転している間は自分の銃を撃たず、撃つのは同乗者の座席 (銃手) に任せる。座席に留まる間は撃つ判断まで進まないので、押していた引き金をここで離す。
            ReleaseTrigger();
            _wheelVehicle = vehicle;
            if (!NeedsBotPilot(vehicle) || !TakeStick(vehicle))
            {
                TakeSteeringWheel(vehicle);
            }

            var route = ChooseDriveRoute(_orderRoute, vehicle);
            ApplyWheelRoute(route);
            Debug.Log(VehicleLog.BotTakesControls(vehicle.name, Id, IsLive(_wheelPilot), route != null ? route.name : null), this);
        }

        // 乗り物に操縦の部品が置いてあればそれを借りるのは、ゲームが機体ごとに決めた操縦の値で飛ばすため。
        // 操縦の部品を握れなかったときに false を返し、運転の部品に任せるのは、乗った Bot が舵を誰にも渡さずに座り続けないようにするため。
        private bool TakeStick(Vehicle vehicle)
        {
            var type = vehicle.GetComponent<IBotPilotedMotor>().BotPilotType;
            if (!IsBotPilotType(type))
            {
                Debug.LogWarning(VehicleLog.BotPilotTypeInvalid(vehicle.name, type != null ? type.FullName : null), this);
                return false;
            }

            var pilot = (IBotPilot)FindLivePart(vehicle.gameObject, type);
            _ownsWheel = pilot == null;
            if (_ownsWheel)
            {
                // 型が正しくても付かないことがあるのは、RequireComponent を満たせないときなどに AddComponent が null を返すため。
                pilot = AddWheelPart(vehicle.gameObject, type) as IBotPilot;
                if (!IsLive(pilot))
                {
                    _ownsWheel = false;
                    Debug.LogWarning(VehicleLog.BotPilotNotAdded(vehicle.name, type.FullName), this);
                    return false;
                }
            }
            else
            {
                _wheelPreviousEnabled = pilot.enabled;
                pilot.enabled = true;
                _wheelPreviousRider = ReferenceEquals(pilot.Rider, pilot) ? null : pilot.Rider;
                _wheelPreviousWaypoints = CopyTransforms(pilot.Waypoints);
            }

            pilot.Rider = this;
            _wheelPilot = pilot;
            return true;
        }

        // 乗り物か自分に運転の部品が置いてあればそれを借りるのは、ゲームが決めた走り方の値で走らせるため。
        private void TakeSteeringWheel(Vehicle vehicle)
        {
            var driver = FindLivePart<BotVehicleDriver>(vehicle.gameObject) ?? FindLivePart<BotVehicleDriver>(gameObject);

            _ownsWheel = driver == null;
            if (_ownsWheel)
            {
                driver = AddWheelPart<BotVehicleDriver>(gameObject);
            }
            else
            {
                _wheelPreviousEnabled = driver.enabled;
                driver.enabled = true;
                _wheelPreviousVehicle = driver.Vehicle;
                _wheelPreviousRider = driver.Rider;
                _wheelPreviousRoute = driver.Route;
                _wheelPreviousUnmanned = driver.DrivesUnmanned;
            }

            // 代わりの運転手を先に止めるのは、運転席に座った自分を差し置いて、空いた座席を探しに行かせないため。
            driver.DrivesUnmanned = false;
            driver.SetVehicle(vehicle);
            driver.SetRider(this);
            _wheelDriver = driver;
        }

        private void RefreshWheelRoute()
        {
            if (!IsDrivingVehicle)
            {
                return;
            }

            var route = ChooseDriveRoute(_orderRoute, _wheelVehicle);
            if (ReferenceEquals(route, _wheelRoute))
            {
                return;
            }

            ApplyWheelRoute(route);
            Debug.Log(VehicleLog.BotTakesControls(VehicleName(_wheelVehicle), Id, IsLive(_wheelPilot), route != null ? route.name : null), this);
        }

        // 道が無いときに借りた部品の道へ戻すのは、ゲームがその部品に置いた道を、乗った敵が消してしまわないようにするため。
        private void ApplyWheelRoute(VehicleRoute route)
        {
            _wheelRoute = route;
            if (_wheelDriver != null)
            {
                _wheelDriver.SetRoute(route != null || _ownsWheel ? route : _wheelPreviousRoute);
            }

            if (IsLive(_wheelPilot))
            {
                if (route != null)
                {
                    route.CopyPoints(_wheelPoints);
                    _wheelPilot.SetWaypoints(_wheelPoints.ToArray());
                    _wheelPoints.Clear();
                }
                else
                {
                    _wheelPilot.SetWaypoints(_ownsWheel ? Array.Empty<Transform>() : _wheelPreviousWaypoints);
                }

                // 自分で付けた操縦の部品は置いたときの高さを保つ既定のままだと、地上で乗った機体が地面すれすれを飛ぶので、道の点の高さをなぞらせる。
                if (_ownsWheel)
                {
                    _wheelPilot.FollowWaypointHeight = route != null;
                }
            }
        }

        private void GiveUpWheel(bool destroying = false)
        {
            if (ReferenceEquals(_wheelDriver, null) && ReferenceEquals(_wheelPilot, null))
            {
                return;
            }

            var vehicle = _wheelVehicle;
            if (_wheelDriver != null)
            {
                GiveUpSteeringWheel(_wheelDriver, destroying);
            }

            if (IsLive(_wheelPilot))
            {
                GiveUpStick(_wheelPilot);
            }

            _wheelVehicle = null;
            _wheelDriver = null;
            _wheelPilot = null;
            _wheelRoute = null;
            _ownsWheel = false;
            _wheelPreviousVehicle = null;
            _wheelPreviousRider = null;
            _wheelPreviousRoute = null;
            _wheelPreviousUnmanned = false;
            _wheelPreviousWaypoints = Array.Empty<Transform>();
            Debug.Log(VehicleLog.BotGivesUpControls(VehicleName(vehicle), Id), this);
        }

        private void GiveUpSteeringWheel(BotVehicleDriver driver, bool destroying)
        {
            if (_ownsWheel)
            {
                // 消すのはコマの終わりまで待つので、その間に Update で乗り物を走らせないよう先に止める。
                driver.SetRider(null);
                driver.enabled = false;

                // 自分で付けた部品は自分と同じ GameObject にあり、自分が消えるときは一緒に消えるので、消している途中に二重に消さない。
                if (!destroying)
                {
                    DestroyWheelPart(driver);
                }

                return;
            }

            driver.SetRider(_wheelPreviousRider);
            driver.SetVehicle(_wheelPreviousVehicle);
            driver.SetRoute(_wheelPreviousRoute);
            driver.DrivesUnmanned = _wheelPreviousUnmanned;
            driver.enabled = _wheelPreviousEnabled;
        }

        private void GiveUpStick(IBotPilot pilot)
        {
            pilot.Release();
            if (_ownsWheel)
            {
                // 乗り手を外すと自分で操縦席に座りに行くので、乗り手は残したまま止めて消す。
                pilot.enabled = false;
                DestroyWheelPart((Component)pilot);
                return;
            }

            pilot.Rider = _wheelPreviousRider;
            pilot.SetWaypoints(_wheelPreviousWaypoints);
            pilot.enabled = _wheelPreviousEnabled;
        }

        // 消える予定の部品は借りない。Play 中の Destroy はコマの終わりまで待つので、同じコマに座り直すと止まった部品を借りてしまうため。
        private static T FindLivePart<T>(GameObject host) where T : MonoBehaviour
        {
            return (T)FindLivePart(host, typeof(T));
        }

        private static Component FindLivePart(GameObject host, Type type)
        {
            foreach (var part in host.GetComponents(type))
            {
                if (part != null && !PendingWheelParts.Contains(part))
                {
                    return part;
                }
            }

            return null;
        }

        private static T AddWheelPart<T>(GameObject host) where T : MonoBehaviour
        {
            return (T)AddWheelPart(host, typeof(T));
        }

        // DisallowMultipleComponent の部品は、消える予定の古い部品が残っていると AddComponent が null を返すので、先に消し切る。
        private static Component AddWheelPart(GameObject host, Type type)
        {
            foreach (var part in host.GetComponents(type))
            {
                if (part != null && PendingWheelParts.Remove(part))
                {
                    DestroyImmediate(part);
                }
            }

            PendingWheelParts.RemoveWhere(p => p == null);
            return host.AddComponent(type);
        }

        // interface への null 判定は破棄済みの MonoBehaviour を見逃すので、Unity の null で見る。
        private static bool IsLive(IBotPilot pilot)
        {
            return pilot is UnityEngine.Object unityPilot ? unityPilot != null : pilot != null;
        }

        private static Transform[] CopyTransforms(IReadOnlyList<Transform> source)
        {
            if (source == null || source.Count == 0)
            {
                return Array.Empty<Transform>();
            }

            var copy = new Transform[source.Count];
            for (var i = 0; i < copy.Length; i++)
            {
                copy[i] = source[i];
            }

            return copy;
        }

        private static string VehicleName(Vehicle vehicle)
        {
            return vehicle != null ? vehicle.name : "-";
        }

        private static void DestroyWheelPart(Component part)
        {
            if (Application.isPlaying)
            {
                PendingWheelParts.Add(part);
                Destroy(part);
            }
            else
            {
                DestroyImmediate(part);
            }
        }
    }
}

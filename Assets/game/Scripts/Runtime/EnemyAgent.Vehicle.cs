using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// <see cref="EnemyAgent"/> のうち、乗り物に乗る処理 (<see cref="IVehicleRider"/>)。敵とモードの Bot は入力を持たないので、
    /// 乗り降りはコードから (<see cref="TryBoard(Vehicle)"/>・<see cref="TryLeaveVehicle"/>・<see cref="TrySwitchVehicleSeat"/>) 行う。
    /// 乗っている間は NavMeshAgent を止めて体を座席に留め、判断と移動を進めない (座席から撃つ・動かすのは座席の入力の口を使う振る舞いが足す)。
    /// 倒れたら降りる。
    /// </summary>
    public sealed partial class EnemyAgent : IVehicleRider
    {
        private readonly RiderVisibility _vehicleHidden = new RiderVisibility();
        private VehicleSeat _vehicleSeat;

        public VehicleSeat Seat => _vehicleSeat;

        public bool IsRiding => _vehicleSeat != null;

        /// <summary>乗っている乗り物。乗っていなければ null。</summary>
        public Vehicle Vehicle => _vehicleSeat != null ? _vehicleSeat.Vehicle : null;

        string IVehicleRider.RiderName => Id;

        Transform IVehicleRider.RiderRoot => transform;

        float IVehicleRider.FeetBelowRoot => _agent != null ? _agent.baseOffset : 0f;

        float IVehicleRider.ClearanceRadius => _agent != null ? _agent.radius : 0.5f;

        float IVehicleRider.ClearanceHeight => _agent != null ? _agent.height : 2f;

        bool IVehicleRider.CanBoard => enabled && gameObject.activeInHierarchy && _brain != null && !_brain.IsDead;

        /// <summary>vehicle の乗り込める座席 (一番近い空席) に乗る。乗れなければ false (理由は乗り物がログに出す)。</summary>
        public bool TryBoard(Vehicle vehicle)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            return vehicle.TryEnter(this);
        }

        /// <summary>
        /// vehicle の seatIndex の座席に乗る。requireInRange が false なら離れていても乗る (モードが出現と同時に座らせるとき)。
        /// 乗れなければ false。
        /// </summary>
        public bool TryBoard(Vehicle vehicle, int seatIndex, bool requireInRange = true)
        {
            if (vehicle == null)
            {
                throw new ArgumentNullException(nameof(vehicle));
            }

            return vehicle.TryEnter(this, seatIndex, requireInRange);
        }

        /// <summary>乗っていれば降りる (降りる場所がすべて塞がっていれば降りない)。降りたら true。</summary>
        public bool TryLeaveVehicle()
        {
            var vehicle = Vehicle;
            return vehicle != null && vehicle.TryExit(this);
        }

        /// <summary>乗っていれば seatIndex の座席に移る。移ったら true。</summary>
        public bool TrySwitchVehicleSeat(int seatIndex)
        {
            var vehicle = Vehicle;
            return vehicle != null && vehicle.TrySwitchToSeat(this, seatIndex);
        }

        void IVehicleRider.OnBoarded(VehicleSeat seat)
        {
            _vehicleSeat = seat ?? throw new ArgumentNullException(nameof(seat));
            StopForVehicle();
            ApplySeatPose(seat);
            FollowSeat();
        }

        void IVehicleRider.OnSeatChanged(VehicleSeat from, VehicleSeat to)
        {
            _vehicleSeat = to ?? throw new ArgumentNullException(nameof(to));
            ApplySeatPose(to);
            FollowSeat();
        }

        void IVehicleRider.OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
        {
            _vehicleSeat = null;

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

        // 乗っている間は判断も移動も進めず、体を座席に付いていかせる。
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
    }
}

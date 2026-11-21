using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class VehicleTestRider : MonoBehaviour, IVehicleRider
    {
        public string RiderName => name;

        public Transform RiderRoot => transform;

        public float FeetBelowRoot => 1f;

        public float ClearanceRadius => 0.4f;

        public float ClearanceHeight => 1.8f;

        public bool CanBoard => true;

        public VehicleSeat Seat { get; private set; }

        public void OnBoarded(VehicleSeat seat)
        {
            Seat = seat;
        }

        public void OnSeatChanged(VehicleSeat from, VehicleSeat to)
        {
            Seat = to;
        }

        public void OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation)
        {
            Seat = null;
        }
    }
}

using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    public interface IVehicleRider
    {
        string RiderName { get; }

        Transform RiderRoot { get; }

        float FeetBelowRoot { get; }

        float ClearanceRadius { get; }

        float ClearanceHeight { get; }

        bool CanBoard { get; }

        VehicleSeat Seat { get; }

        void OnBoarded(VehicleSeat seat);

        void OnSeatChanged(VehicleSeat from, VehicleSeat to);

        void OnLeft(VehicleSeat seat, VehicleLeaveReason reason, Vector3 feetPosition, Quaternion rotation);
    }
}

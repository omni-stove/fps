using Shooter.Core;
using Shooter.Runtime;
using UnityEngine;

namespace Shooter.Tests.EditMode
{
    public sealed class RollFromAimScheme : MonoBehaviour, IVehicleControlScheme
    {
        public const int RollAxis = 5;

        public VehicleSeat LastSeat { get; private set; }

        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            LastSeat = seat;
            return VehicleMotorInput.Idle.With(RollAxis, input.AimDirection.x);
        }
    }
}

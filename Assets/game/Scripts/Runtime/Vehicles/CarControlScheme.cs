using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class CarControlScheme : IVehicleControlScheme
    {
        public static readonly CarControlScheme Instance = new CarControlScheme();

        public VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds)
        {
            return VehicleMotorInput.Idle
                .With(CarDrive.ThrottleAxis, input.Move.y)
                .With(CarDrive.SteerAxis, input.Move.x)
                .With(CarDrive.BrakeAxis, input.Boost ? 1f : 0f)
                .With(CarDrive.RightAxis, input.Vertical);
        }
    }
}

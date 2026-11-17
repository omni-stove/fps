namespace Shooter.Runtime
{
    public interface IVehicleInputSink
    {
        void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds);

        void OnSeatVacated(VehicleSeat seat);
    }
}

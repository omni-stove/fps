using Shooter.Core;

namespace Shooter.Runtime
{
    // 乗り物の GameObject に付ける部品で差し替えるのは、オフラインの座席 (VehicleMotorLink) とオンラインの運転 (NetV2Vehicles) が同じ当て方を通るようにするため。
    // 経た時間を渡すのは、スロットルの開き具合のように押している間に変わる状態を、当て方が持てるようにするため。
    public interface IVehicleControlScheme
    {
        VehicleMotorInput ToMotorInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds);
    }
}

namespace Shooter.Runtime
{
    // PlayerController.MoveFilter と同じく差し込みにするのは、乗っている人の入力を自動の確認・チュートリアル・補助が書き換えるとき、
    // PlayerVehicleRider を作り直さずに済むようにするため。
    public interface IVehicleSeatInputFilter
    {
        void Filter(VehicleSeat seat, ref VehicleSeatInput input);
    }
}

namespace Fps.Runtime
{
    /// <summary>
    /// 座席の入力を受け取って、乗り物の動き方や武器に使うもの。<see cref="Vehicle.AddInputSink"/> で登録すると、
    /// どの座席の入力も届くので、使う座席 (<see cref="VehicleSeat.Role"/> や <see cref="VehicleSeat.Index"/>) を自分で選ぶ。
    /// </summary>
    public interface IVehicleInputSink
    {
        /// <summary>seat に座っている人が、このフレームの入力を出した。</summary>
        void OnSeatInput(VehicleSeat seat, VehicleSeatInput input, float deltaSeconds);

        /// <summary>
        /// seat が空いた (降りた・移った・乗り物が壊れた)。押されたままの入力 (アクセル・引き金など) をここで放す。
        /// </summary>
        void OnSeatVacated(VehicleSeat seat);
    }
}

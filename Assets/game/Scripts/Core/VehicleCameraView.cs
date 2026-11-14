namespace Fps.Core
{
    /// <summary>座席に座っている間のカメラ。</summary>
    public enum VehicleCameraView
    {
        /// <summary>乗り物の後ろから追う三人称のカメラ。</summary>
        Chase = 0,

        /// <summary>座席からの視点 (座席に視点の位置があればそこ、無ければ乗っている人の目)。</summary>
        Seat = 1,
    }
}

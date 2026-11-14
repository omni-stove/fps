namespace Fps.Core
{
    /// <summary>乗り物の座席の種類。</summary>
    public enum VehicleSeatRole
    {
        /// <summary>運転席。乗り物の動き方に入力を流す。1 台に 1 つまで。</summary>
        Driver = 0,

        /// <summary>助手席。</summary>
        Passenger = 1,

        /// <summary>銃座。乗り物の武器に入力を流す。</summary>
        Gunner = 2,
    }
}

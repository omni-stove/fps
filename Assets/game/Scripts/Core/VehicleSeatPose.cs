namespace Fps.Core
{
    /// <summary>座席に座っている間のキャラクターの姿勢。</summary>
    public enum VehicleSeatPose
    {
        /// <summary>座る。当たり判定はしゃがみの高さにする。</summary>
        Seated = 0,

        /// <summary>立ったまま (荷台や銃座)。</summary>
        Standing = 1,

        /// <summary>車内に隠れて外から見えない。体を描かず、当たり判定はしゃがみの高さにする。</summary>
        Hidden = 2,
    }
}

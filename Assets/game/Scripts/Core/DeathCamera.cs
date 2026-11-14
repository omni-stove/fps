namespace Fps.Core
{
    /// <summary>
    /// 倒されてからリスポーンするまで、自分のカメラに何を映すか。
    /// </summary>
    public enum DeathCamera
    {
        /// <summary>特別なカメラを使わない。</summary>
        None,

        /// <summary>自分を倒した相手を映す。</summary>
        Killer,

        /// <summary>生きている味方を映す。</summary>
        Ally,

        /// <summary>自由に動かせるカメラにする。</summary>
        Free,

        /// <summary>
        /// 倒した相手の倒す直前の数秒 (<see cref="KillCamClip"/>) をその肩越しで再生してから、<see cref="Killer"/> と同じに映す。
        /// 相手の記録が無ければすぐ <see cref="Killer"/> にする。値を保存・同期に使うので、最後に足している。
        /// </summary>
        KillCam,
    }
}

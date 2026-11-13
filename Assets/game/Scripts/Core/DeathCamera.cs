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
    }
}

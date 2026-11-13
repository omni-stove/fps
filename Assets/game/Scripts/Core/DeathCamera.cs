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
    }
}

namespace Fps.Core
{
    /// <summary>
    /// 時間の速さを変えられるか。全員の時間が変わるので、オフラインに限る (オンラインでは On でも変えない)。
    /// </summary>
    public enum SlowMotion
    {
        /// <summary>変えない。</summary>
        Off,

        /// <summary>オフラインでは変えられる。</summary>
        On,
    }
}

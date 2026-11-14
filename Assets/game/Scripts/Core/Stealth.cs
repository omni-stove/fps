namespace Fps.Core
{
    /// <summary>
    /// 敵がプレイヤーにすぐ気づかず、発見ゲージを溜めて気づき・警戒・交戦と段階を踏むか。
    /// </summary>
    public enum Stealth
    {
        /// <summary>見えたらすぐ交戦する。</summary>
        Off,

        /// <summary>発見ゲージで段階を踏む。</summary>
        On,
    }
}

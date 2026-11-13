namespace Fps.Core
{
    /// <summary>
    /// <see cref="HoldMode"/> を選べる操作。<see cref="ControlSettings"/> が操作ごとにモードを持つ。
    /// </summary>
    public enum HoldAction
    {
        /// <summary>ダッシュ。</summary>
        Sprint,

        /// <summary>しゃがみ。</summary>
        Crouch,

        /// <summary>覗き込み (ADS)。</summary>
        Aim,
    }
}

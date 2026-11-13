namespace Fps.Core
{
    /// <summary>
    /// HUD にどれだけの情報を出すか。
    /// </summary>
    public enum HudDensity
    {
        /// <summary>すべて出す。</summary>
        Full,

        /// <summary>戦闘に要るものだけに絞る。</summary>
        Minimal,

        /// <summary>ほとんど出さない。</summary>
        Hardcore,
    }
}

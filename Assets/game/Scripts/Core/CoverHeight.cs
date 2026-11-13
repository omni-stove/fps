namespace Fps.Core
{
    /// <summary>カバーにした壁の高さの区分。</summary>
    public enum CoverHeight
    {
        /// <summary>上端が <see cref="CoverProbe.LowWallHeight"/> 未満。しゃがんで隠れ、立って覗く。</summary>
        Low,

        /// <summary>上端が <see cref="CoverProbe.LowWallHeight"/> 以上。立って隠れ、端から横に出て覗く。</summary>
        High,
    }
}

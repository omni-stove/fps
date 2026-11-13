using System;

namespace Fps.Core
{
    /// <summary>壁沿いにあと <see cref="CoverProbe.EdgeProbeDistance"/> 動くと壁が途切れる側。左右は壁に向いたときの左右。</summary>
    [Flags]
    public enum CoverEdge
    {
        /// <summary>左右とも壁が続く。</summary>
        None = 0,

        /// <summary>左で壁が途切れる。</summary>
        Left = 1,

        /// <summary>右で壁が途切れる。</summary>
        Right = 2,
    }
}

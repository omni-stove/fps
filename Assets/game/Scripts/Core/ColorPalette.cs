namespace Fps.Core
{
    /// <summary>
    /// 敵味方と注意を引く印に使う色の組 (<see cref="TeamPalette"/>) の種類。設定 (<see cref="ControlSettings.Palette"/>) で選ぶ。
    /// </summary>
    public enum ColorPalette
    {
        /// <summary>味方は青・敵は赤。</summary>
        Standard,

        /// <summary>赤と緑を見分けにくい人 (1 型・2 型色覚) 向け。味方は青・敵は橙で、ネームプレートに形の印も付ける。</summary>
        RedGreen,

        /// <summary>青と黄を見分けにくい人 (3 型色覚) 向け。味方は青緑・敵は赤で、ネームプレートに形の印も付ける。</summary>
        BlueYellow,
    }
}

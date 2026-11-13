namespace Fps.Core
{
    /// <summary>
    /// 遮蔽物に隠れたまま、狙わずに撃てるか。
    /// </summary>
    public enum BlindFire
    {
        /// <summary>隠れたままでは撃てない。</summary>
        Off,

        /// <summary>隠れたまま撃てる。弾は武器ごとの角度で散らばる。</summary>
        On,
    }
}

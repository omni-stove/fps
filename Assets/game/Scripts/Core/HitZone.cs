namespace Fps.Core
{
    /// <summary>
    /// 弾が当たった部位。部位ごとのダメージ倍率は <see cref="DamageCalculator"/> が持つ。
    /// </summary>
    public enum HitZone
    {
        /// <summary>胴体。倍率 1.0。</summary>
        Body,

        /// <summary>頭。倍率 2.0。</summary>
        Head,

        /// <summary>手足。倍率 0.75。</summary>
        Limb,
    }
}

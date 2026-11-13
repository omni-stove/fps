namespace Fps.Core
{
    /// <summary>
    /// グレネードを投げられるか。
    /// </summary>
    public enum Grenade
    {
        /// <summary>投げられない。</summary>
        Off,

        /// <summary>出撃ごとに <see cref="ActionRules.GrenadeCount"/> 個まで投げられる。</summary>
        On,
    }
}

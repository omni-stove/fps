namespace Fps.Core
{
    /// <summary>爆破 (<see cref="SearchAndDestroy"/>) の爆弾の状態。</summary>
    public enum BombState
    {
        /// <summary>攻撃側の誰かが持っている。</summary>
        Carried,

        /// <summary>持ち主が倒れるか抜けて、地面に落ちている。攻撃側が拾える。</summary>
        Dropped,

        /// <summary>設置地点に設置され、起爆を待っている。防衛側が解除できる。</summary>
        Planted,

        /// <summary>防衛側が解除した。</summary>
        Defused,

        /// <summary>起爆した。</summary>
        Detonated,
    }
}

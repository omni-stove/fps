namespace Fps.Core
{
    /// <summary><see cref="PickupEligibility"/> の、陣営で拾える人を絞るやり方。</summary>
    public enum PickupSideRule
    {
        /// <summary>誰でも拾える。陣営を持たない人も拾える。</summary>
        Anyone = 0,

        /// <summary>指定した陣営の人だけが拾える。陣営を持たない人は拾えない。</summary>
        OnlySide = 1,

        /// <summary>指定した陣営以外の人が拾える。陣営を持たない人も拾える。</summary>
        ExceptSide = 2,
    }
}

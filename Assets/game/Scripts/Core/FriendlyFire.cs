namespace Fps.Core
{
    /// <summary>
    /// 味方に弾が当たったときの扱い。
    /// </summary>
    public enum FriendlyFire
    {
        /// <summary>味方には被害を与えない。</summary>
        Off,

        /// <summary>敵と同じように味方にも被害を与える。</summary>
        On,

        /// <summary>味方には被害を与えず、撃った本人に跳ね返す。</summary>
        Reflect,
    }
}

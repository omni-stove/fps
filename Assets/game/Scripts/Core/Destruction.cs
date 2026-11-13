namespace Fps.Core
{
    /// <summary>
    /// 建造物を弾で壊せるか。
    /// </summary>
    public enum Destruction
    {
        /// <summary>建造物は壊れない。</summary>
        Off,

        /// <summary>弾が当たった建造物に被害を与える。</summary>
        On,
    }
}

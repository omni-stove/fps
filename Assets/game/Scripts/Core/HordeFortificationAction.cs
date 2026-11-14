namespace Fps.Core
{
    /// <summary>ホードの防衛物に対して、通貨を払ってできること。</summary>
    public enum HordeFortificationAction
    {
        /// <summary>何もできない (壊れていない)。</summary>
        None,

        /// <summary>まだ置かれていない防衛物を置く。</summary>
        Build,

        /// <summary>壊れた部品を直す。</summary>
        Repair,
    }
}

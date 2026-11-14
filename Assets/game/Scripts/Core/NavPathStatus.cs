namespace Fps.Core
{
    /// <summary><see cref="NavPath"/> がどこまで届いているか。</summary>
    public enum NavPathStatus
    {
        /// <summary>経路が無い (出発点が面の上に無い、求める前など)。</summary>
        Invalid,

        /// <summary>目的地には届かず、いちばん近づける所までの経路。</summary>
        Partial,

        /// <summary>目的地まで届く経路。</summary>
        Complete,
    }
}

namespace Fps.Core
{
    /// <summary>カバーの状態 (<see cref="CoverState.Phase"/>)。</summary>
    public enum CoverPhase
    {
        /// <summary>カバーの外。</summary>
        Out,

        /// <summary>壁へ寄せている途中 (<see cref="CoverState.EnterSeconds"/> の間)。</summary>
        Entering,

        /// <summary>壁に張り付いて隠れている。</summary>
        In,

        /// <summary>張り付いたまま覗いている (低い壁なら立つ、高い壁なら端から横に出る)。</summary>
        Peeking,
    }
}

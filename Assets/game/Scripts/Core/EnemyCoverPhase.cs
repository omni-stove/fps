namespace Fps.Core
{
    /// <summary><see cref="EnemyState.TakeCover"/> の中の段階 (<see cref="EnemyBrain.CoverPhase"/>)。</summary>
    public enum EnemyCoverPhase
    {
        /// <summary>TakeCover ではない。</summary>
        None,

        /// <summary>遮蔽点へ向かっている。</summary>
        Moving,

        /// <summary>遮蔽点でしゃがんで隠れている。</summary>
        Hiding,

        /// <summary>遮蔽点で立って覗き、見えていれば撃つ。</summary>
        Peeking,
    }
}

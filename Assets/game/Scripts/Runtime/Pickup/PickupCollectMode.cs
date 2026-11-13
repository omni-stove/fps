namespace Fps.Runtime
{
    /// <summary><see cref="Pickup"/> をどう拾うか。</summary>
    public enum PickupCollectMode
    {
        /// <summary>範囲に入ったら拾う。範囲にいる間に拾えるようになれば (撃たれて体力が減ったなど) その時点で拾う。</summary>
        Touch = 0,

        /// <summary>範囲にいるプレイヤーが Interact (キーボードは E の長押し) で拾う。bot は拾わない。</summary>
        Interact = 1,
    }
}

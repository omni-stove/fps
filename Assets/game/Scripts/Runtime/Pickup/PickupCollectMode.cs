namespace Fps.Runtime
{
    /// <summary><see cref="Pickup"/> をどう拾うか。</summary>
    public enum PickupCollectMode
    {
        /// <summary>範囲に入ったら拾う。範囲にいる間に拾えるようになれば (撃たれて体力が減ったなど) その時点で拾う。</summary>
        Touch = 0,

        /// <summary>範囲にいるプレイヤーが Interact (キーボードは E の長押し) で拾う。bot は拾わない。</summary>
        Interact = 1,

        /// <summary>
        /// 範囲にいるプレイヤーが見た目に照準 (画面の中央) を合わせて Interact で拾う。見た目には照準の線だけが当たる判定を付け、回さずに地面に置く。
        /// 落とした武器はこの拾い方にする (触れて拾うと、落とした人がその場で拾い直してしまうため)。bot は拾わない。
        /// </summary>
        Look = 2,
    }
}

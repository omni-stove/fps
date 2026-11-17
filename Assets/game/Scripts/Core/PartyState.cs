namespace Shooter.Core
{
    /// <summary>パーティのリーダーが、メンバー用ロビーの自分の属性 (<see cref="PartyLobby.StateKey"/>) に書く今の状態。</summary>
    public enum PartyState
    {
        /// <summary>試合を探していない。属性がまだ無いときもこれ。</summary>
        Idle,

        /// <summary>リーダーが入る試合を探している。</summary>
        Searching,

        /// <summary>リーダーが入るサーバーを決めた。メンバーは同じサーバーに入る。</summary>
        InMatch,
    }
}

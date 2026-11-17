namespace Shooter.NetV2
{
    /// <summary><see cref="PartySession"/> がパーティに入っているかの段階。</summary>
    public enum PartyPhase
    {
        /// <summary>パーティに入っていない。</summary>
        None,

        /// <summary>ログインしてパーティを作っている、または招待のコードで入ろうとしている。</summary>
        Joining,

        /// <summary>パーティに入っている。</summary>
        InParty,

        /// <summary>パーティから出ようとしている。</summary>
        Leaving,
    }
}

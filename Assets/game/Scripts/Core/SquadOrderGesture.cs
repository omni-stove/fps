namespace Fps.Core
{
    /// <summary><see cref="SquadOrderButton.Tick"/> が返す、SquadCommand のボタンの押し方。</summary>
    public enum SquadOrderGesture
    {
        /// <summary>この tick には何も決まっていない。</summary>
        None,

        /// <summary>長押しになる前に離した。照準の先に応じて指示を出す。</summary>
        Tap,

        /// <summary>押し続けて長押しになった。指示を取り消して付いてこさせる。</summary>
        Hold,
    }
}

namespace Fps.Core
{
    /// <summary>分隊への指示を出さなかった理由。</summary>
    public enum SquadOrderRefusal
    {
        /// <summary>出した。</summary>
        None,

        /// <summary>指示する人が倒れている。</summary>
        Dead,

        /// <summary>照準を決める視点が無い。</summary>
        NoView,

        /// <summary>指示する人に陣営が無く、味方の bot が決まらない。</summary>
        NoSide,

        /// <summary>照準の先の届く距離に、地面も敵も無い。</summary>
        NoTarget,

        /// <summary>指示を受ける味方の bot がいない。</summary>
        NoRecipients,
    }
}

namespace Fps.Core
{
    /// <summary><see cref="KillConfirmed.Collect"/> でタグを拾おうとした結果。</summary>
    public enum KillConfirmedResult
    {
        /// <summary>敵のタグを拾った。拾った人の陣営に 1 点入り、タグは無くなる。</summary>
        Confirmed = 0,

        /// <summary>味方 (自分を含む) のタグを拾った。点は入らずにタグが無くなる (相手の確定を阻止した)。</summary>
        Denied = 1,

        /// <summary>タグはもう無い (拾われたか消えた)。何も変わらない。</summary>
        Gone = 2,

        /// <summary>試合が Playing でない。何も変わらない。</summary>
        NotPlaying = 3,

        /// <summary>拾おうとした人が倒れている。何も変わらない。</summary>
        CollectorDown = 4,
    }
}

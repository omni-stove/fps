using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 参加者をどの陣営 (<see cref="Side"/>) に分けるか。<see cref="ModeSession"/> が参加順の番号と今の各陣営の人数を渡して呼ぶ。
    /// 実装は状態を持たず、同じ入力なら同じ陣営を返す。
    /// </summary>
    public interface ITeamLayout
    {
        /// <summary>
        /// 参加者がいなくても始めからある陣営 (ID の昇順)。得点を 0 で持ち、時間切れの比べ合いに入る。
        /// FFA のように参加者が来るたびに陣営ができるなら空。
        /// </summary>
        IReadOnlyList<Side> FixedSides { get; }

        /// <summary>1 つの陣営に入れる人数の上限。上限がなければ null。</summary>
        int? Capacity { get; }

        /// <summary>side がこのチーム分けで使える陣営なら true。</summary>
        bool Contains(Side side);

        /// <summary>
        /// joinIndex 番目 (0 から数え、抜けた人も数えたまま) に参加した人の陣営を選ぶ。
        /// memberCount で今の各陣営の人数を引ける。どの陣営にも入れなければ null。
        /// </summary>
        Side? Assign(int joinIndex, Func<Side, int> memberCount);
    }
}

using Fps.Core;

namespace Fps.Runtime
{
    /// <summary>拠点制圧 (<see cref="Domination"/>) の表示。両チームのポイント・残り時間・各拠点の持ち主と、終わったら勝者を出す。</summary>
    public interface IDominationHud
    {
        /// <summary>出している試合。出していなければ null。</summary>
        Domination Source { get; }

        /// <summary>domination を出し始める。前に出していた試合は手放す。null なら <see cref="Clear"/> と同じ。</summary>
        void Show(Domination domination);

        /// <summary>出すのをやめて隠す。</summary>
        void Clear();

        /// <summary>試合の今の値を読み直して出す。</summary>
        void Refresh();
    }
}

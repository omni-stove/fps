namespace Fps.Core
{
    /// <summary><see cref="PredictionBuffer{TInput, TState}.Reconcile"/> の結果の種類。</summary>
    public enum ReconcileOutcome
    {
        /// <summary>その tick の予測がもう (またはまだ) 無いので何もしなかった。古い・重複した確定状態など。</summary>
        Ignored,

        /// <summary>予測と確定状態のずれが許容範囲内だったので何もしなかった。</summary>
        Matched,

        /// <summary>ずれが許容範囲を超えたので、確定状態に巻き戻して以降の入力を再シミュレーションした。</summary>
        Corrected,
    }
}

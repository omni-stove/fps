namespace Fps.Core
{
    /// <summary><see cref="PredictionBuffer{TInput, TState}.Reconcile"/> の結果。</summary>
    public readonly struct ReconcileResult
    {
        public ReconcileOutcome Outcome { get; }

        /// <summary>照合した tick。</summary>
        public int Tick { get; }

        /// <summary>予測と確定状態のずれ。<see cref="ReconcileOutcome.Ignored"/> のときは 0。</summary>
        public float Error { get; }

        /// <summary>再シミュレーションした tick 数。<see cref="ReconcileOutcome.Corrected"/> 以外では 0。</summary>
        public int ResimulatedTicks { get; }

        public ReconcileResult(ReconcileOutcome outcome, int tick, float error, int resimulatedTicks)
        {
            Outcome = outcome;
            Tick = tick;
            Error = error;
            ResimulatedTicks = resimulatedTicks;
        }
    }
}

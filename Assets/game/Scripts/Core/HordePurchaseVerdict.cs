namespace Fps.Core
{
    /// <summary>ホードで通貨を払って防衛物を直す・置こうとした結果 (<see cref="Horde.CheckPurchase"/>)。</summary>
    public enum HordePurchaseVerdict
    {
        /// <summary>払えた (払える)。</summary>
        Ok,

        /// <summary>試合中ではない。</summary>
        NotPlaying,

        /// <summary>波と戦っている間なので買えない (<see cref="HordeSpec.PurchasesOnlyBetweenWaves"/>)。</summary>
        WaveInProgress,

        /// <summary>通貨が足りない。</summary>
        NotEnoughCurrency,

        /// <summary>近くに直す・置くものが無い (直せる部品が人に塞がれているときも含む)。</summary>
        NothingToDo,
    }
}

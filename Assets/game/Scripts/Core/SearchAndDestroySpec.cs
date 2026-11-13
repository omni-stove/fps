using System;

namespace Fps.Core
{
    /// <summary>
    /// 爆破 (<see cref="SearchAndDestroy"/>) の設定。生成時に値を検査し、以後は変わらない。
    /// </summary>
    public sealed class SearchAndDestroySpec
    {
        public const int DefaultSiteCount = 2;
        public const float DefaultRoundSeconds = 120f;
        public const float DefaultPlantSeconds = 3f;
        public const float DefaultDefuseSeconds = 5f;
        public const float DefaultFuseSeconds = 30f;
        public const int DefaultWinsToWin = 4;
        public const float DefaultPreparationSeconds = 5f;
        public const int DefaultSwapEveryRounds = 2;

        /// <summary>設置地点の数。1 以上。HUD とログでは 0 = A, 1 = B, ...。</summary>
        public int SiteCount { get; }

        /// <summary>1 ラウンドの制限時間 (秒)。設置するとそこで止まる。0 より大きい有限値。</summary>
        public float RoundSeconds { get; }

        /// <summary>設置し終えるまで長押しを続ける秒数。0 より大きい有限値。</summary>
        public float PlantSeconds { get; }

        /// <summary>解除し終えるまで長押しを続ける秒数。0 より大きい有限値。</summary>
        public float DefuseSeconds { get; }

        /// <summary>設置してから起爆するまでの秒数。0 より大きい有限値。</summary>
        public float FuseSeconds { get; }

        /// <summary>試合に勝つのに要るラウンドの勝ち数。1 以上。</summary>
        public int WinsToWin { get; }

        /// <summary>各ラウンドの開始前の待ち (秒)。0 以上の有限値。</summary>
        public float PreparationSeconds { get; }

        /// <summary>何ラウンドごとに攻守を入れ替えるか。0 なら入れ替えない。</summary>
        public int SwapEveryRounds { get; }

        /// <exception cref="ArgumentOutOfRangeException">どれかの値が範囲の外。</exception>
        public SearchAndDestroySpec(
            int siteCount = DefaultSiteCount,
            float roundSeconds = DefaultRoundSeconds,
            float plantSeconds = DefaultPlantSeconds,
            float defuseSeconds = DefaultDefuseSeconds,
            float fuseSeconds = DefaultFuseSeconds,
            int winsToWin = DefaultWinsToWin,
            float preparationSeconds = DefaultPreparationSeconds,
            int swapEveryRounds = DefaultSwapEveryRounds)
        {
            if (siteCount < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(siteCount), siteCount, "siteCount must be at least 1");
            }

            RequirePositiveFinite(roundSeconds, nameof(roundSeconds));
            RequirePositiveFinite(plantSeconds, nameof(plantSeconds));
            RequirePositiveFinite(defuseSeconds, nameof(defuseSeconds));
            RequirePositiveFinite(fuseSeconds, nameof(fuseSeconds));

            if (winsToWin < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(winsToWin), winsToWin, "winsToWin must be at least 1");
            }

            if (!(preparationSeconds >= 0f) || float.IsInfinity(preparationSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(preparationSeconds), preparationSeconds, "preparationSeconds must not be negative and must be finite");
            }

            if (swapEveryRounds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(swapEveryRounds), swapEveryRounds, "swapEveryRounds must not be negative");
            }

            SiteCount = siteCount;
            RoundSeconds = roundSeconds;
            PlantSeconds = plantSeconds;
            DefuseSeconds = defuseSeconds;
            FuseSeconds = fuseSeconds;
            WinsToWin = winsToWin;
            PreparationSeconds = preparationSeconds;
            SwapEveryRounds = swapEveryRounds;
        }

        private static void RequirePositiveFinite(float value, string name)
        {
            if (!(value > 0f) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, value, name + " must be positive and finite");
            }
        }
    }
}

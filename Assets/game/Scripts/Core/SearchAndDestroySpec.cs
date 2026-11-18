using System;

namespace Shooter.Core
{
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
        public const float DefaultBombPickupRadius = 1.5f;
        public const float DefaultDefuseRadius = 2f;

        public static SearchAndDestroySpec Default { get; } = new SearchAndDestroySpec();

        public int SiteCount { get; }

        public float RoundSeconds { get; }

        public float PlantSeconds { get; }

        public float DefuseSeconds { get; }

        public float FuseSeconds { get; }

        public int WinsToWin { get; }

        public float PreparationSeconds { get; }

        public int SwapEveryRounds { get; }

        public float BombPickupRadius { get; }

        public float DefuseRadius { get; }

        public SearchAndDestroySpec(
            int siteCount = DefaultSiteCount,
            float roundSeconds = DefaultRoundSeconds,
            float plantSeconds = DefaultPlantSeconds,
            float defuseSeconds = DefaultDefuseSeconds,
            float fuseSeconds = DefaultFuseSeconds,
            int winsToWin = DefaultWinsToWin,
            float preparationSeconds = DefaultPreparationSeconds,
            int swapEveryRounds = DefaultSwapEveryRounds,
            float bombPickupRadius = DefaultBombPickupRadius,
            float defuseRadius = DefaultDefuseRadius)
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

            RequirePositiveFinite(bombPickupRadius, nameof(bombPickupRadius));
            RequirePositiveFinite(defuseRadius, nameof(defuseRadius));

            SiteCount = siteCount;
            RoundSeconds = roundSeconds;
            PlantSeconds = plantSeconds;
            DefuseSeconds = defuseSeconds;
            FuseSeconds = fuseSeconds;
            WinsToWin = winsToWin;
            PreparationSeconds = preparationSeconds;
            SwapEveryRounds = swapEveryRounds;
            BombPickupRadius = bombPickupRadius;
            DefuseRadius = defuseRadius;
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

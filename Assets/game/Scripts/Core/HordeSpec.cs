using System;

namespace Shooter.Core
{
    public sealed class HordeSpec
    {
        public const int DefaultEnemySideId = 1;
        public const int DefaultStartingCurrency = 50;
        public const int DefaultKillReward = 10;
        public const int DefaultKillRewardPerWave = 2;
        public const int DefaultKillScore = 1;
        public const int DefaultBuildCost = 40;
        public const int DefaultRepairCostPerPiece = 4;
        public const float DefaultInteractRadius = 3.5f;
        public const float DefaultAlertInterval = 3f;
        public const float DefaultMinSpawnDistance = 8f;
        public const float DefaultCorpseSeconds = 5f;

        public HordeSpec(
            Side defenders,
            Side attackers,
            int startingCurrency = DefaultStartingCurrency,
            int killReward = DefaultKillReward,
            int killRewardPerWave = DefaultKillRewardPerWave,
            bool purchasesOnlyBetweenWaves = true,
            int buildCost = DefaultBuildCost,
            int repairCostPerPiece = DefaultRepairCostPerPiece,
            float interactRadius = DefaultInteractRadius,
            int killScore = DefaultKillScore,
            float alertInterval = DefaultAlertInterval,
            float minSpawnDistance = DefaultMinSpawnDistance,
            float corpseSeconds = DefaultCorpseSeconds)
        {
            if (defenders == attackers)
            {
                throw new ArgumentException($"defenders and attackers must differ, but both are {defenders}", nameof(attackers));
            }

            if (startingCurrency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(startingCurrency), startingCurrency, "startingCurrency must not be negative");
            }

            if (killReward < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killReward), killReward, "killReward must not be negative");
            }

            if (killRewardPerWave < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killRewardPerWave), killRewardPerWave, "killRewardPerWave must not be negative");
            }

            if (buildCost < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(buildCost), buildCost, "buildCost must not be negative");
            }

            if (repairCostPerPiece < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(repairCostPerPiece), repairCostPerPiece, "repairCostPerPiece must not be negative");
            }

            // NaN はどの比較も false なので、否定で書いて NaN も弾く。
            if (!(interactRadius > 0f) || float.IsInfinity(interactRadius))
            {
                throw new ArgumentOutOfRangeException(nameof(interactRadius), interactRadius, "interactRadius must be positive and finite");
            }

            if (killScore < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(killScore), killScore, "killScore must not be negative");
            }

            if (!(alertInterval > 0f) || float.IsInfinity(alertInterval))
            {
                throw new ArgumentOutOfRangeException(nameof(alertInterval), alertInterval, "alertInterval must be positive and finite");
            }

            if (!(minSpawnDistance >= 0f) || float.IsInfinity(minSpawnDistance))
            {
                throw new ArgumentOutOfRangeException(nameof(minSpawnDistance), minSpawnDistance, "minSpawnDistance must be non-negative and finite");
            }

            if (!(corpseSeconds >= 0f) || float.IsInfinity(corpseSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(corpseSeconds), corpseSeconds, "corpseSeconds must be non-negative and finite");
            }

            Defenders = defenders;
            Attackers = attackers;
            StartingCurrency = startingCurrency;
            KillReward = killReward;
            KillRewardPerWave = killRewardPerWave;
            PurchasesOnlyBetweenWaves = purchasesOnlyBetweenWaves;
            BuildCost = buildCost;
            RepairCostPerPiece = repairCostPerPiece;
            InteractRadius = interactRadius;
            KillScore = killScore;
            AlertInterval = alertInterval;
            MinSpawnDistance = minSpawnDistance;
            CorpseSeconds = corpseSeconds;
        }

        public Side Defenders { get; }

        public Side Attackers { get; }

        public int StartingCurrency { get; }

        public int KillReward { get; }

        public int KillRewardPerWave { get; }

        public bool PurchasesOnlyBetweenWaves { get; }

        public int BuildCost { get; }

        public int RepairCostPerPiece { get; }

        public float InteractRadius { get; }

        public int KillScore { get; }

        public float AlertInterval { get; }

        public float MinSpawnDistance { get; }

        public float CorpseSeconds { get; }

        public int RepairCost(int pieceCount)
        {
            if (pieceCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(pieceCount), pieceCount, "pieceCount must not be negative");
            }

            return (int)Math.Min(int.MaxValue, (long)RepairCostPerPiece * pieceCount);
        }

        public int KillRewardAt(int waveNumber)
        {
            if (waveNumber < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber), waveNumber, "waveNumber must be at least 1");
            }

            var reward = KillReward + ((long)KillRewardPerWave * (waveNumber - 1));
            return reward >= int.MaxValue ? int.MaxValue : (int)reward;
        }
    }
}

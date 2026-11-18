using System;
using System.Globalization;

namespace Shooter.Core
{
    // 感染者の武器を Id で持つのは、Unity 抜きのサーバーが WeaponDefinition を読めず、武器を Id で引くため。
    // 空を認めるのは、感染しても持っている武器のままにするゲームに Id を書かせないため。
    public sealed class InfectedSpec
    {
        public const string DefaultInfectedWeaponId = "";

        public const int DefaultInitialInfected = 1;
        public const float DefaultStartDelaySeconds = 10f;
        public const float DefaultRoundSeconds = 300f;
        public const int DefaultInfectedMaxHealth = 150;
        public const float DefaultInfectedSpeedMultiplier = 1.15f;
        public const float DefaultInfectedRespawnSeconds = 3f;
        public const int DefaultScorePerInfection = 1;
        public const int DefaultScorePerSurvivedRound = 3;

        public static InfectedSpec Default { get; } = new InfectedSpec();

        public InfectedSpec(
            int initialInfected = DefaultInitialInfected,
            float startDelaySeconds = DefaultStartDelaySeconds,
            float roundSeconds = DefaultRoundSeconds,
            int infectedMaxHealth = DefaultInfectedMaxHealth,
            float infectedSpeedMultiplier = DefaultInfectedSpeedMultiplier,
            float infectedRespawnSeconds = DefaultInfectedRespawnSeconds,
            int scorePerInfection = DefaultScorePerInfection,
            int scorePerSurvivedRound = DefaultScorePerSurvivedRound,
            string infectedWeaponId = DefaultInfectedWeaponId)
        {
            InitialInfected = SpecGuard.AtLeast(initialInfected, 1, nameof(initialInfected));
            StartDelaySeconds = SpecGuard.NonNegativeFinite(startDelaySeconds, nameof(startDelaySeconds));
            RoundSeconds = SpecGuard.PositiveFinite(roundSeconds, nameof(roundSeconds));

            // 感染者を選ぶ前に終わるラウンドは、生存者が何もせずに勝つ。
            if (StartDelaySeconds >= RoundSeconds)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(startDelaySeconds),
                    startDelaySeconds,
                    nameof(startDelaySeconds) + " must be shorter than roundSeconds (" + roundSeconds.ToString(CultureInfo.InvariantCulture) + ")");
            }

            InfectedMaxHealth = SpecGuard.AtLeast(infectedMaxHealth, 1, nameof(infectedMaxHealth));
            InfectedSpeedMultiplier = SpecGuard.PositiveFinite(infectedSpeedMultiplier, nameof(infectedSpeedMultiplier));
            InfectedRespawnSeconds = SpecGuard.NonNegativeFinite(infectedRespawnSeconds, nameof(infectedRespawnSeconds));
            ScorePerInfection = SpecGuard.NonNegative(scorePerInfection, nameof(scorePerInfection));
            ScorePerSurvivedRound = SpecGuard.NonNegative(scorePerSurvivedRound, nameof(scorePerSurvivedRound));
            InfectedWeaponId = infectedWeaponId == null ? string.Empty : infectedWeaponId.Trim();
        }

        public int InitialInfected { get; }

        public float StartDelaySeconds { get; }

        public float RoundSeconds { get; }

        public int InfectedMaxHealth { get; }

        public float InfectedSpeedMultiplier { get; }

        public float InfectedRespawnSeconds { get; }

        public int ScorePerInfection { get; }

        public int ScorePerSurvivedRound { get; }

        public string InfectedWeaponId { get; }

        public bool HasInfectedWeapon => InfectedWeaponId.Length > 0;
    }
}

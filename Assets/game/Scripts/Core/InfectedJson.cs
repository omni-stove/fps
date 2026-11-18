using System.Collections.Generic;

namespace Shooter.Core
{
    public static class InfectedJson
    {
        public const string ResourcePath = "Tuning/infected";

        public const string InitialInfectedItem = "initialInfected";
        public const string StartDelaySecondsItem = "startDelaySeconds";
        public const string RoundSecondsItem = "roundSeconds";
        public const string InfectedMaxHealthItem = "infectedMaxHealth";
        public const string InfectedSpeedMultiplierItem = "infectedSpeedMultiplier";
        public const string InfectedRespawnSecondsItem = "infectedRespawnSeconds";
        public const string ScorePerInfectionItem = "scorePerInfection";
        public const string ScorePerSurvivedRoundItem = "scorePerSurvivedRound";

        private const string _kind = "infected JSON";

        public static IReadOnlyList<string> ItemNames { get; } = new[]
        {
            InitialInfectedItem,
            StartDelaySecondsItem,
            RoundSecondsItem,
            InfectedMaxHealthItem,
            InfectedSpeedMultiplierItem,
            InfectedRespawnSecondsItem,
            ScorePerInfectionItem,
            ScorePerSurvivedRoundItem,
        };

        public static InfectedSpec Parse(string text, string sourceName)
        {
            return SpecJsonReader.Read(text, sourceName, _kind, ItemNames, fields =>
            {
                var initialInfected = fields.ReadInt(InitialInfectedItem, InfectedSpec.DefaultInitialInfected);
                var startDelaySeconds = fields.ReadFloat(StartDelaySecondsItem, InfectedSpec.DefaultStartDelaySeconds);
                var roundSeconds = fields.ReadFloat(RoundSecondsItem, InfectedSpec.DefaultRoundSeconds);
                var infectedMaxHealth = fields.ReadInt(InfectedMaxHealthItem, InfectedSpec.DefaultInfectedMaxHealth);
                var infectedSpeedMultiplier = fields.ReadFloat(InfectedSpeedMultiplierItem, InfectedSpec.DefaultInfectedSpeedMultiplier);
                var infectedRespawnSeconds = fields.ReadFloat(InfectedRespawnSecondsItem, InfectedSpec.DefaultInfectedRespawnSeconds);
                var scorePerInfection = fields.ReadInt(ScorePerInfectionItem, InfectedSpec.DefaultScorePerInfection);
                var scorePerSurvivedRound = fields.ReadInt(ScorePerSurvivedRoundItem, InfectedSpec.DefaultScorePerSurvivedRound);
                return SpecJsonReader.Build(
                    fields,
                    ItemNames,
                    () => new InfectedSpec(
                        initialInfected,
                        startDelaySeconds,
                        roundSeconds,
                        infectedMaxHealth,
                        infectedSpeedMultiplier,
                        infectedRespawnSeconds,
                        scorePerInfection,
                        scorePerSurvivedRound));
            });
        }
    }
}

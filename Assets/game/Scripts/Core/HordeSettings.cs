using System;

namespace Shooter.Core
{
    // 専用サーバーのルールのファイル (match.horde) が持つ Horde の調整値。陣営は持たない (サーバーは守る側を Alpha、敵を Bravo に決める)。
    // 出現点の距離 (HordeSpec.MinSpawnDistance) はオフラインの WaveSpawner が出現点を選ぶ値で、サーバーは Session の出現の選び方を使うので持たない。
    public sealed class HordeSettings : IEquatable<HordeSettings>
    {
        public const int DefaultWaveCount = 5;
        public const float DefaultRestSeconds = 5f;
        public const int DefaultFirstEnemyCount = 3;
        public const float DefaultEnemiesPerWave = 2f;
        public const float DefaultFirstStrength = 1f;
        public const float DefaultStrengthPerWave = 0.25f;
        public const float DefaultMaxStrength = 3f;
        public const int DefaultMaxAlive = 4;

        // 陣営は値を確かめるためだけに使う。ToSpec が呼ぶ側の陣営で作り直す
        private static readonly Side _checkDefenders = Side.Alpha;
        private static readonly Side _checkAttackers = Side.Bravo;

        public HordeSettings(
            int startingCurrency = HordeSpec.DefaultStartingCurrency,
            int killReward = HordeSpec.DefaultKillReward,
            int killRewardPerWave = HordeSpec.DefaultKillRewardPerWave,
            bool purchasesOnlyBetweenWaves = true,
            int killScore = HordeSpec.DefaultKillScore,
            int buildCost = HordeSpec.DefaultBuildCost,
            int repairCostPerPiece = HordeSpec.DefaultRepairCostPerPiece,
            float interactRadius = HordeSpec.DefaultInteractRadius,
            float corpseSeconds = HordeSpec.DefaultCorpseSeconds,
            float alertInterval = HordeSpec.DefaultAlertInterval,
            int waveCount = DefaultWaveCount,
            float restSeconds = DefaultRestSeconds,
            int firstEnemyCount = DefaultFirstEnemyCount,
            float enemiesPerWave = DefaultEnemiesPerWave,
            float firstStrength = DefaultFirstStrength,
            float strengthPerWave = DefaultStrengthPerWave,
            float maxStrength = DefaultMaxStrength,
            int maxAlive = DefaultMaxAlive)
        {
            if (waveCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(waveCount), waveCount, "waveCount must not be negative (0 is endless)");
            }

            StartingCurrency = startingCurrency;
            KillReward = killReward;
            KillRewardPerWave = killRewardPerWave;
            PurchasesOnlyBetweenWaves = purchasesOnlyBetweenWaves;
            KillScore = killScore;
            BuildCost = buildCost;
            RepairCostPerPiece = repairCostPerPiece;
            InteractRadius = interactRadius;
            CorpseSeconds = corpseSeconds;
            AlertInterval = alertInterval;
            WaveCount = waveCount;
            RestSeconds = restSeconds;
            FirstEnemyCount = firstEnemyCount;
            EnemiesPerWave = enemiesPerWave;
            FirstStrength = firstStrength;
            StrengthPerWave = strengthPerWave;
            MaxStrength = maxStrength;
            MaxAlive = maxAlive;

            // 引数の名前は HordeSpec・WaveFormula の引数と同じなので、ルールのファイルの読み手はその名前で項目を指せる
            ToSpec(_checkDefenders, _checkAttackers);
            ToFormula();
        }

        public static HordeSettings Default { get; } = new HordeSettings();

        public int StartingCurrency { get; }

        public int KillReward { get; }

        public int KillRewardPerWave { get; }

        public bool PurchasesOnlyBetweenWaves { get; }

        public int KillScore { get; }

        public int BuildCost { get; }

        public int RepairCostPerPiece { get; }

        public float InteractRadius { get; }

        // 倒された敵の Bot を試合から抜くまでの秒数
        public float CorpseSeconds { get; }

        // 敵の Bot を、いちばん近い守る側へ向け直す間隔 (秒)
        public float AlertInterval { get; }

        // 0 なら終わりが無い
        public int WaveCount { get; }

        public float RestSeconds { get; }

        public int FirstEnemyCount { get; }

        public float EnemiesPerWave { get; }

        public float FirstStrength { get; }

        public float StrengthPerWave { get; }

        public float MaxStrength { get; }

        public int MaxAlive { get; }

        public HordeSpec ToSpec(Side defenders, Side attackers, float minSpawnDistance = HordeSpec.DefaultMinSpawnDistance)
        {
            return new HordeSpec(
                defenders,
                attackers,
                StartingCurrency,
                KillReward,
                KillRewardPerWave,
                PurchasesOnlyBetweenWaves,
                BuildCost,
                RepairCostPerPiece,
                InteractRadius,
                KillScore,
                AlertInterval,
                minSpawnDistance,
                CorpseSeconds);
        }

        public WaveFormula ToFormula()
        {
            return new WaveFormula(FirstEnemyCount, EnemiesPerWave, FirstStrength, StrengthPerWave, MaxAlive, RestSeconds, MaxStrength);
        }

        // 波の予定は進むと戻せないので、試合ごとに作り直す
        public WaveSchedule CreateSchedule()
        {
            return WaveSchedule.FromFormula(ToFormula(), WaveCount);
        }

        public bool Equals(HordeSettings other)
        {
            if (other is null)
            {
                return false;
            }

            return StartingCurrency == other.StartingCurrency
                && KillReward == other.KillReward
                && KillRewardPerWave == other.KillRewardPerWave
                && PurchasesOnlyBetweenWaves == other.PurchasesOnlyBetweenWaves
                && KillScore == other.KillScore
                && BuildCost == other.BuildCost
                && RepairCostPerPiece == other.RepairCostPerPiece
                && InteractRadius.Equals(other.InteractRadius)
                && CorpseSeconds.Equals(other.CorpseSeconds)
                && AlertInterval.Equals(other.AlertInterval)
                && WaveCount == other.WaveCount
                && RestSeconds.Equals(other.RestSeconds)
                && FirstEnemyCount == other.FirstEnemyCount
                && EnemiesPerWave.Equals(other.EnemiesPerWave)
                && FirstStrength.Equals(other.FirstStrength)
                && StrengthPerWave.Equals(other.StrengthPerWave)
                && MaxStrength.Equals(other.MaxStrength)
                && MaxAlive == other.MaxAlive;
        }

        public override bool Equals(object obj) => obj is HordeSettings other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                var hash = StartingCurrency;
                hash = (hash * 397) ^ KillReward;
                hash = (hash * 397) ^ WaveCount;
                hash = (hash * 397) ^ FirstEnemyCount;
                hash = (hash * 397) ^ MaxAlive;
                hash = (hash * 397) ^ RestSeconds.GetHashCode();
                return hash;
            }
        }
    }
}

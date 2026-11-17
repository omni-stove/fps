using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Horde", menuName = "Shooter/Horde")]
    public sealed class HordeDefinition : ScriptableObject
    {
        [Header("通貨")]
        [SerializeField, Min(0), Tooltip("始めに持っている通貨")]
        private int _startingCurrency = HordeSpec.DefaultStartingCurrency;

        [SerializeField, Min(0), Tooltip("1 波目で敵を 1 体倒したときに入る通貨")]
        private int _killReward = HordeSpec.DefaultKillReward;

        [SerializeField, Min(0), Tooltip("波が 1 つ進むごとに、1 体あたりの通貨に足す量")]
        private int _killRewardPerWave = HordeSpec.DefaultKillRewardPerWave;

        [SerializeField, Tooltip("防衛物を直す・置くのを波の間 (と最初の波の前) だけにするか")]
        private bool _purchasesOnlyBetweenWaves = true;

        [SerializeField, Min(0), Tooltip("敵を 1 体倒したときに守る側の得点に足す点")]
        private int _killScore = HordeSpec.DefaultKillScore;

        [Header("補強")]
        [SerializeField, Min(0), Tooltip("まだ置かれていない防衛物を置くときに払う通貨。すべての防衛物に使う")]
        private int _buildCost = HordeSpec.DefaultBuildCost;

        [SerializeField, Min(0), Tooltip("壊れた部品 1 つを直すときに払う通貨。すべての防衛物に使う")]
        private int _repairCostPerPiece = HordeSpec.DefaultRepairCostPerPiece;

        [SerializeField, Min(0.1f), Tooltip("防衛物の真ん中からこの水平の距離 (m) 以内にいる人が直す・置く操作をできる。すべての防衛物に使う")]
        private float _interactRadius = HordeSpec.DefaultInteractRadius;

        [Header("敵")]
        [SerializeField, Min(0), Tooltip("波の敵の陣営の ID。守る側 (プレイヤー) の陣営と違うこと")]
        private int _enemySideId = HordeSpec.DefaultEnemySideId;

        [SerializeField, Min(0.1f), Tooltip("Idle・Patrol の敵をいちばん近い敵対者へ向かわせる間隔 (秒)")]
        private float _alertInterval = HordeSpec.DefaultAlertInterval;

        [SerializeField, Min(0f), Tooltip("敵対者からこの距離 (m) 以内の出現点は、ほかに使える点があれば使わない")]
        private float _minSpawnDistance = HordeSpec.DefaultMinSpawnDistance;

        [SerializeField, Min(0f), Tooltip("倒された敵を消すまでの秒数。0 なら消さない")]
        private float _corpseSeconds = HordeSpec.DefaultCorpseSeconds;

        [Header("波")]
        [SerializeField, Min(0), Tooltip("波の数。最後の波を全滅させたら勝ち。0 なら終わりが無い")]
        private int _waveCount = WaveSpawner.DefaultWaveCount;

        [SerializeField, Min(0f), Tooltip("波の間の休憩 (秒)。倒れた味方が戻り、防衛物を直す時間")]
        private float _restSeconds = WaveSpawner.DefaultRestSeconds;

        [SerializeField, Min(1), Tooltip("1 波目の敵の数")]
        private int _firstEnemyCount = WaveSpawner.DefaultFirstEnemyCount;

        [SerializeField, Min(0f), Tooltip("波ごとに増える敵の数")]
        private float _enemiesPerWave = WaveSpawner.DefaultEnemiesPerWave;

        [SerializeField, Min(0.01f), Tooltip("1 波目の強さの倍率")]
        private float _firstStrength = WaveSpawner.DefaultFirstStrength;

        [SerializeField, Min(0f), Tooltip("波ごとに増える強さの倍率")]
        private float _strengthPerWave = WaveSpawner.DefaultStrengthPerWave;

        [SerializeField, Min(0.01f), Tooltip("強さの倍率の上限")]
        private float _maxStrength = WaveSpawner.DefaultMaxStrength;

        [SerializeField, Min(1), Tooltip("同時に出しておく敵の上限")]
        private int _maxAlive = WaveSpawner.DefaultMaxAlive;

        public int StartingCurrency
        {
            get => _startingCurrency;
            set => _startingCurrency = value;
        }

        public int KillReward
        {
            get => _killReward;
            set => _killReward = value;
        }

        public int KillRewardPerWave
        {
            get => _killRewardPerWave;
            set => _killRewardPerWave = value;
        }

        public bool PurchasesOnlyBetweenWaves
        {
            get => _purchasesOnlyBetweenWaves;
            set => _purchasesOnlyBetweenWaves = value;
        }

        public int KillScore
        {
            get => _killScore;
            set => _killScore = value;
        }

        public int BuildCost
        {
            get => _buildCost;
            set => _buildCost = value;
        }

        public int RepairCostPerPiece
        {
            get => _repairCostPerPiece;
            set => _repairCostPerPiece = value;
        }

        public float InteractRadius
        {
            get => _interactRadius;
            set => _interactRadius = value;
        }

        public Side EnemySide
        {
            get => new Side(Math.Max(0, _enemySideId));
            set => _enemySideId = value.Id;
        }

        public float AlertInterval
        {
            get => _alertInterval;
            set => _alertInterval = value;
        }

        public float MinSpawnDistance
        {
            get => _minSpawnDistance;
            set => _minSpawnDistance = value;
        }

        public float CorpseSeconds
        {
            get => _corpseSeconds;
            set => _corpseSeconds = value;
        }

        public int WaveCount
        {
            get => _waveCount;
            set => _waveCount = value;
        }

        public float RestSeconds
        {
            get => _restSeconds;
            set => _restSeconds = value;
        }

        public int FirstEnemyCount
        {
            get => _firstEnemyCount;
            set => _firstEnemyCount = value;
        }

        public float EnemiesPerWave
        {
            get => _enemiesPerWave;
            set => _enemiesPerWave = value;
        }

        public float FirstStrength
        {
            get => _firstStrength;
            set => _firstStrength = value;
        }

        public float StrengthPerWave
        {
            get => _strengthPerWave;
            set => _strengthPerWave = value;
        }

        public float MaxStrength
        {
            get => _maxStrength;
            set => _maxStrength = value;
        }

        public int MaxAlive
        {
            get => _maxAlive;
            set => _maxAlive = value;
        }

        public HordeSpec ToSpec(Side defenders, Side attackers)
        {
            try
            {
                return new HordeSpec(
                    defenders,
                    attackers,
                    _startingCurrency,
                    _killReward,
                    _killRewardPerWave,
                    _purchasesOnlyBetweenWaves,
                    _buildCost,
                    _repairCostPerPiece,
                    _interactRadius,
                    _killScore,
                    _alertInterval,
                    _minSpawnDistance,
                    _corpseSeconds);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"horde definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public WaveSchedule CreateSchedule()
        {
            try
            {
                var formula = new WaveFormula(
                    _firstEnemyCount, _enemiesPerWave, _firstStrength, _strengthPerWave, _maxAlive, _restSeconds, _maxStrength);
                return WaveSchedule.FromFormula(formula, _waveCount);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"horde definition '{SourceName}' has invalid waves: {e.Message}", e);
            }
        }

        public void Validate(Side defenders, Side attackers)
        {
            ToSpec(defenders, attackers);
            CreateSchedule();
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed horde definition)" : name;
    }
}

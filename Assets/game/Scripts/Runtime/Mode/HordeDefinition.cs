using System;
using Fps.Core;
using UnityEngine;

namespace Fps.Runtime
{
    /// <summary>
    /// ホードの調整値 (通貨の入り方・防衛物を買える時期・波の式) をアセットとして持つ。<see cref="HordeRunner"/> が読み、
    /// <see cref="ToSpec"/> で <see cref="HordeSpec"/>、<see cref="CreateSchedule"/> で波の予定を作る。
    /// 新しく作ったときの値は <see cref="HordeSpec"/> と <see cref="WaveSpawner"/> の既定値と同じ。
    /// 中身は Fps.Editor.HordeSceneBuilder が無いときだけ作る。
    /// </summary>
    [CreateAssetMenu(fileName = "Horde", menuName = "Fps/Horde")]
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

        /// <summary>defenders が守り attackers が攻めるときの <see cref="HordeSpec"/> を作る。</summary>
        /// <exception cref="ArgumentException">値が不正か、defenders と attackers が同じ。メッセージにアセット名を含む。</exception>
        public HordeSpec ToSpec(Side defenders, Side attackers)
        {
            try
            {
                return new HordeSpec(defenders, attackers, _startingCurrency, _killReward, _killRewardPerWave, _purchasesOnlyBetweenWaves);
            }
            catch (ArgumentException e)
            {
                throw new ArgumentException($"horde definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        /// <summary>波の式から、まだ始まっていない波の予定を作る。呼ぶたびに新しいインスタンスを返す。</summary>
        /// <exception cref="ArgumentException">波の値が不正。メッセージにアセット名を含む。</exception>
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

        /// <summary>defenders と attackers で <see cref="ToSpec"/> と <see cref="CreateSchedule"/> が通ることを確かめる。</summary>
        /// <exception cref="ArgumentException">どちらかが通らない。</exception>
        public void Validate(Side defenders, Side attackers)
        {
            ToSpec(defenders, attackers);
            CreateSchedule();
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed horde definition)" : name;
    }
}

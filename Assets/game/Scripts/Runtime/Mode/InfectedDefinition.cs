using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "Infected", menuName = "Shooter/Modes/Infected")]
    public sealed class InfectedDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("始まりの秒数が過ぎたときに生存者から選ぶ最初の感染者の数。生存者は必ず 1 人は残る")]
        private int _initialInfected = InfectedRunner.DefaultInitialInfected;

        [SerializeField, Min(0f), Tooltip("最初の感染者を選ぶまでの秒数。それまでは全員が生存者")]
        private float _startDelaySeconds = InfectedRunner.DefaultStartDelaySeconds;

        [SerializeField, Min(1f), Tooltip("制限時間 (秒)。切れたときに生存者が残っていれば生存者の勝ち")]
        private float _roundSeconds = InfectedRunner.DefaultRoundSeconds;

        [SerializeField, Tooltip("感染者に持たせる武器。空なら持っている武器のまま")]
        private WeaponDefinition _infectedWeapon;

        [SerializeField, Min(1), Tooltip("感染者の最大体力")]
        private int _infectedMaxHealth = InfectedRunner.DefaultInfectedMaxHealth;

        [SerializeField, Min(0.01f), Tooltip("感染者 (bot) の移動の速さの倍率")]
        private float _infectedSpeedMultiplier = InfectedRunner.DefaultInfectedSpeedMultiplier;

        [SerializeField, Min(0f), Tooltip("倒れてから出現点に戻るまでの秒数")]
        private float _infectedRespawnSeconds = InfectedRunner.DefaultInfectedRespawnSeconds;

        [SerializeField, Min(0), Tooltip("感染者が生存者を 1 人感染させたときに入る点")]
        private int _scorePerInfection = InfectedRunner.DefaultScorePerInfection;

        [SerializeField, Min(0), Tooltip("時間切れまで生き残ったときに生存者に入る点")]
        private int _scorePerSurvivedRound = InfectedRunner.DefaultScorePerSurvivedRound;

        public int InitialInfected
        {
            get => _initialInfected;
            set => _initialInfected = value;
        }

        public float StartDelaySeconds
        {
            get => _startDelaySeconds;
            set => _startDelaySeconds = value;
        }

        public float RoundSeconds
        {
            get => _roundSeconds;
            set => _roundSeconds = value;
        }

        public WeaponDefinition InfectedWeapon
        {
            get => _infectedWeapon;
            set => _infectedWeapon = value;
        }

        public int InfectedMaxHealth
        {
            get => _infectedMaxHealth;
            set => _infectedMaxHealth = value;
        }

        public float InfectedSpeedMultiplier
        {
            get => _infectedSpeedMultiplier;
            set => _infectedSpeedMultiplier = value;
        }

        public float InfectedRespawnSeconds
        {
            get => _infectedRespawnSeconds;
            set => _infectedRespawnSeconds = value;
        }

        public int ScorePerInfection
        {
            get => _scorePerInfection;
            set => _scorePerInfection = value;
        }

        public int ScorePerSurvivedRound
        {
            get => _scorePerSurvivedRound;
            set => _scorePerSurvivedRound = value;
        }

        public InfectedSpec ToSpec()
        {
            var weaponId = InfectedSpec.DefaultInfectedWeaponId;
            if (_infectedWeapon != null)
            {
                try
                {
                    weaponId = _infectedWeapon.ToSpec().Id;
                }
                catch (WeaponDefinitionException e)
                {
                    throw new InvalidOperationException($"infected definition '{SourceName}' has an invalid infected weapon: {e.Message}", e);
                }
            }

            try
            {
                return new InfectedSpec(
                    _initialInfected,
                    _startDelaySeconds,
                    _roundSeconds,
                    _infectedMaxHealth,
                    _infectedSpeedMultiplier,
                    _infectedRespawnSeconds,
                    _scorePerInfection,
                    _scorePerSurvivedRound,
                    weaponId);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException($"infected definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        // 武器は Id から WeaponDefinition へ戻すのにカタログが要るので写さない。InfectedWeapon で別に入れる。
        public void CopyFrom(InfectedSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _initialInfected = spec.InitialInfected;
            _startDelaySeconds = spec.StartDelaySeconds;
            _roundSeconds = spec.RoundSeconds;
            _infectedMaxHealth = spec.InfectedMaxHealth;
            _infectedSpeedMultiplier = spec.InfectedSpeedMultiplier;
            _infectedRespawnSeconds = spec.InfectedRespawnSeconds;
            _scorePerInfection = spec.ScorePerInfection;
            _scorePerSurvivedRound = spec.ScorePerSurvivedRound;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed infected definition)" : name;
    }
}

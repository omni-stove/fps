using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [CreateAssetMenu(fileName = "ExplosiveProp", menuName = "Shooter/Explosive Prop")]
    public sealed class ExplosivePropDefinition : ScriptableObject
    {
        [SerializeField, Min(1), Tooltip("耐久。被害でこれが 0 になると爆発する (燃えるなら燃え始める)")]
        private int _maxHealth = ExplosivePropSpec.DefaultMaxHealth;

        [SerializeField, Min(0), Tooltip("爆発の中心で人に与えるダメージ")]
        private int _maxDamage = ExplosivePropSpec.DefaultMaxDamage;

        [SerializeField, Min(0.01f), Tooltip("爆発が届く半径 (m)。建造物とほかの置き物への被害もこの半径")]
        private float _damageRadius = ExplosivePropSpec.DefaultDamageRadius;

        [SerializeField, Min(0f), Tooltip("減らさずに最大のダメージを与える半径 (m)。届く半径より小さくする")]
        private float _fullDamageRadius = ExplosivePropSpec.DefaultFullDamageRadius;

        [SerializeField, Min(0), Tooltip("爆発の中心で建造物とほかの置き物に与える被害。0 なら連鎖もしない")]
        private int _structureDamageAtCenter = ExplosivePropSpec.DefaultStructureDamageAtCenter;

        [SerializeField, Min(0f), Tooltip("ほかの置き物の爆発で耐久が 0 になったとき、爆発するまで待つ秒数の下限")]
        private float _chainDelaySeconds = ExplosivePropSpec.DefaultChainDelaySeconds;

        [SerializeField, Min(0f), Tooltip("耐久が 0 になってから燃え続け、爆発するまでの秒数。0 なら燃えずに爆発する")]
        private float _burnSeconds = ExplosivePropSpec.DefaultBurnSeconds;

        public int MaxHealth
        {
            get => _maxHealth;
            set => _maxHealth = value;
        }

        public int MaxDamage
        {
            get => _maxDamage;
            set => _maxDamage = value;
        }

        public float DamageRadius
        {
            get => _damageRadius;
            set => _damageRadius = value;
        }

        public float FullDamageRadius
        {
            get => _fullDamageRadius;
            set => _fullDamageRadius = value;
        }

        public int StructureDamageAtCenter
        {
            get => _structureDamageAtCenter;
            set => _structureDamageAtCenter = value;
        }

        public float ChainDelaySeconds
        {
            get => _chainDelaySeconds;
            set => _chainDelaySeconds = value;
        }

        public float BurnSeconds
        {
            get => _burnSeconds;
            set => _burnSeconds = value;
        }

        public ExplosivePropSpec ToSpec()
        {
            try
            {
                return new ExplosivePropSpec(
                    _maxHealth,
                    _maxDamage,
                    _damageRadius,
                    _fullDamageRadius,
                    _structureDamageAtCenter,
                    _chainDelaySeconds,
                    _burnSeconds);
            }
            catch (ArgumentOutOfRangeException e)
            {
                throw new InvalidOperationException($"explosive prop definition '{SourceName}' is invalid: {e.Message}", e);
            }
        }

        public void CopyFrom(ExplosivePropSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            _maxHealth = spec.MaxHealth;
            _maxDamage = spec.MaxDamage;
            _damageRadius = spec.DamageRadius;
            _fullDamageRadius = spec.FullDamageRadius;
            _structureDamageAtCenter = spec.StructureDamageAtCenter;
            _chainDelaySeconds = spec.ChainDelaySeconds;
            _burnSeconds = spec.BurnSeconds;
        }

        private string SourceName => string.IsNullOrEmpty(name) ? "(unnamed explosive prop definition)" : name;
    }
}

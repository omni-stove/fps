using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class HealthRegen : MonoBehaviour
    {
        private Damageable _target;
        private GameRules _rules;
        private HealthRegenerator _regenerator;

        public Damageable Target => _target;

        public GameRules Rules => _rules;

        public HealthRegenerator Regenerator => _regenerator;

        public bool IsBound => _target != null && _regenerator != null;

        private void Update()
        {
            if (IsBound)
            {
                Tick(Time.deltaTime);
            }
        }

        private void OnDestroy()
        {
            Unbind();
        }

        public void Bind(Damageable target, GameRules rules)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            Unbind();
            _target = target;
            _rules = rules;
            _target.Damaged += OnDamaged;
            _target.HealthReset += OnHealthReset;
            _regenerator = new HealthRegenerator(_target.Health, _rules);
        }

        public void Unbind()
        {
            if (_target != null)
            {
                _target.Damaged -= OnDamaged;
                _target.HealthReset -= OnHealthReset;
            }

            _target = null;
            _rules = null;
            _regenerator = null;
        }

        public int Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            return _regenerator != null ? _regenerator.Tick(deltaSeconds) : 0;
        }

        private void OnDamaged(Damageable _, DamageReport report)
        {
            _regenerator?.NotifyDamaged();
        }

        private void OnHealthReset(Health health)
        {
            // 作り直しは被弾ではないので、待ち時間なしで回復を始める。
            _regenerator = new HealthRegenerator(health, _rules);
        }
    }
}

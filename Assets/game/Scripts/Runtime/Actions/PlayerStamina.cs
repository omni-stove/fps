using System;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    public sealed class PlayerStamina : MonoBehaviour, IStaminaGate
    {
        private PlayerController _mover;
        private Damageable _damageable;
        private StaminaMeter _meter = new StaminaMeter(StaminaSpec.Default);
        private Stamina _rule = Stamina.Off;
        private StaminaStatus? _published;

        public Stamina Rule => _rule;

        public StaminaMeter Meter => _meter;

        public StaminaSpec Spec => _meter.Spec;

        public PlayerController Mover => _mover;

        // Edit Mode では OnEnable が走らず isActiveAndEnabled が false のままなので、enabled を直接見る。
        public bool IsOn => _rule == Stamina.On && enabled;

        public StaminaStatus Status => _meter.Status;

        public bool CanSprint => !IsOn || _meter.CanSprint;

        public float CostOf(StaminaUse use)
        {
            return Spec.CostOf(use);
        }

        public bool CanAfford(StaminaUse use)
        {
            return !IsOn || _meter.CanAfford(use);
        }

        public void Spend(StaminaUse use)
        {
            if (!IsOn)
            {
                return;
            }

            var wasDepleted = _meter.IsDepleted;
            if (!_meter.TrySpend(use))
            {
                return;
            }

            Debug.Log(StaminaLog.FormatSpent(use, CostOf(use), _meter.Status), this);
            if (!wasDepleted && _meter.IsDepleted)
            {
                Debug.Log(StaminaLog.FormatDepleted(_meter.Status), this);
            }

            Publish();
        }

        public void Step(bool sprinting, float deltaSeconds)
        {
            if (!IsOn)
            {
                return;
            }

            var wasDepleted = _meter.IsDepleted;
            var wasFull = _meter.IsFull;
            _meter.Tick(deltaSeconds, sprinting);

            if (!wasDepleted && _meter.IsDepleted)
            {
                Debug.Log(StaminaLog.FormatDepleted(_meter.Status), this);
            }
            else if (wasDepleted && !_meter.IsDepleted)
            {
                Debug.Log(StaminaLog.FormatRecovered(_meter.Status), this);
            }

            if (!wasFull && _meter.IsFull)
            {
                Debug.Log(StaminaLog.FormatFull(_meter.Status), this);
            }

            Publish();
        }

        public void Configure(Stamina rule, StaminaSpec spec)
        {
            if (!Enum.IsDefined(typeof(Stamina), rule))
            {
                throw new ArgumentOutOfRangeException(nameof(rule), rule, "rule is not a defined Stamina");
            }

            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            EnsureMover();

            // 同じ調整値なら作り直さない。ルールを入れ直しただけで、減っていた量が満タンに戻らないように。
            if (!ReferenceEquals(spec, _meter.Spec))
            {
                _meter = new StaminaMeter(spec);
            }

            _rule = rule;
            enabled = rule == Stamina.On;
            Debug.Log(StaminaLog.FormatConfigured(rule, spec), this);

            // Edit Mode では enabled を変えても OnEnable / OnDisable が走らないので、ここでも揃える。
            SyncGate();
        }

        public void BindDamageable(Damageable damageable)
        {
            // 破棄済みでも C# のオブジェクトは残っているので、購読は外せる。
            if (!ReferenceEquals(_damageable, null))
            {
                _damageable.HealthReset -= OnHealthReset;
            }

            _damageable = damageable;
            if (!ReferenceEquals(_damageable, null))
            {
                _damageable.HealthReset += OnHealthReset;
            }
        }

        public void Refill()
        {
            _meter.Refill();
            if (!IsOn)
            {
                return;
            }

            Debug.Log(StaminaLog.FormatRefilled(_meter.Status), this);
            Publish();
        }

        private void Awake()
        {
            EnsureMover();
        }

        private void OnEnable()
        {
            SyncGate();
        }

        private void OnDisable()
        {
            Detach();
        }

        private void OnDestroy()
        {
            BindDamageable(null);
            Detach();
        }

        private void OnHealthReset(Health _)
        {
            Refill();
        }

        private void EnsureMover()
        {
            if (_mover == null)
            {
                _mover = GetComponent<PlayerController>();
            }
        }

        private void SyncGate()
        {
            if (!IsOn)
            {
                Detach();
                return;
            }

            if (_mover != null)
            {
                _mover.StaminaGate = this;
            }

            Publish();
        }

        private void Detach()
        {
            if (_mover != null && ReferenceEquals(_mover.StaminaGate, this))
            {
                _mover.StaminaGate = null;
            }

            if (_published.HasValue)
            {
                GameSignals.Stamina.ClearIfCurrent(_published);
                _published = null;
            }
        }

        // 量が変わらない tick (満タンで歩いているとき) は出し直さない。毎フレーム購読側を起こさないため。
        private void Publish()
        {
            var status = _meter.Status;
            if (_published.HasValue && _published.Value == status)
            {
                return;
            }

            _published = status;
            GameSignals.Stamina.Publish(status);
        }
    }
}

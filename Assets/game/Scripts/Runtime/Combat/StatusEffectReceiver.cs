using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using UnityEngine;

namespace Shooter.Runtime
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Damageable))]
    public sealed partial class StatusEffectReceiver : MonoBehaviour
    {
        public const string LogTag = "[StatusEffect]";

        private readonly StatusEffects _effects = new StatusEffects();
        private readonly HashSet<string> _started = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<StatusEffectTick> _ticks = new List<StatusEffectTick>();
        private readonly List<string> _ended = new List<string>();
        private Damageable _damageable;
        private bool _subscribed;

        public event Action<string> Started;

        public event Action<string> Ended;

        public Damageable Damageable => _damageable != null ? _damageable : (_damageable = GetComponent<Damageable>());

        public IReadOnlyList<ActiveStatusEffect> Active => _effects.Active;

        public bool Has(string kindId) => _effects.Has(kindId);

        public float RemainingSeconds(string kindId) => _effects.RemainingSeconds(kindId);

        public void Afflict(StatusEffectSpec spec, GameObject source, string weaponId)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            // 体力をまだ作っていない体では、最初のダメージで作り直す知らせ (HealthReset) が今かけた分を消してしまうので、先に作る。
            if (Damageable.Health.IsDead)
            {
                return;
            }

            Subscribe();
            var sourceId = source != null ? CombatEvents.OfflineId(source) : (int?)null;
            var effect = _effects.Afflict(spec, sourceId, weaponId);
            Debug.Log(
                LogTag + " afflict target=" + Damageable.Id + " kind=" + effect.KindId + " weapon=" + effect.WeaponId
                + " remaining=" + effect.RemainingSeconds.ToString("F2", CultureInfo.InvariantCulture),
                this);
            BeginKind(effect.KindId);
        }

        public void Advance(float deltaSeconds)
        {
            if (_effects.Count == 0)
            {
                return;
            }

            if (Damageable.IsDead)
            {
                ClearAll();
                return;
            }

            _ticks.Clear();
            _ended.Clear();
            _effects.Tick(deltaSeconds, _ticks, _ended);
            foreach (var tick in _ticks)
            {
                if (Damageable.IsDead)
                {
                    break;
                }

                var attribution = tick.SourceId.HasValue ? new ShotAttribution(tick.SourceId.Value, tick.WeaponId) : (ShotAttribution?)null;
                using (CombatEvents.Attribute(attribution))
                {
                    Damageable.TakeDamage(tick.Damage, null, null, DamageKind.StatusEffect);
                }
            }

            foreach (var kindId in _ended)
            {
                FinishKind(kindId);
            }

            if (Damageable.IsDead)
            {
                ClearAll();
            }
        }

        public void ClearAll()
        {
            _effects.Clear();
            if (_started.Count == 0)
            {
                return;
            }

            foreach (var kindId in new List<string>(_started))
            {
                FinishKind(kindId);
            }
        }

        partial void OnKindStarted(string kindId);

        partial void OnKindEnded(string kindId);

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            if (_subscribed && _damageable != null)
            {
                _damageable.Died -= HandleDied;
                _damageable.HealthReset -= HandleHealthReset;
            }

            _subscribed = false;
        }

        private void Update()
        {
            Advance(Time.deltaTime);
        }

        // Edit Mode のテストでは OnEnable が走らないので、かけるときにも購読を確かめる。
        private void Subscribe()
        {
            if (_subscribed)
            {
                return;
            }

            Damageable.Died += HandleDied;
            Damageable.HealthReset += HandleHealthReset;
            _subscribed = true;
        }

        private void HandleDied(Damageable damageable)
        {
            ClearAll();
        }

        private void HandleHealthReset(Health health)
        {
            ClearAll();
        }

        private void BeginKind(string kindId)
        {
            if (!_started.Add(kindId))
            {
                return;
            }

            StatusEffectVisuals.For(gameObject).Show(kindId);
            OnKindStarted(kindId);
            Started?.Invoke(kindId);
        }

        private void FinishKind(string kindId)
        {
            if (!_started.Remove(kindId))
            {
                return;
            }

            var visuals = GetComponent<StatusEffectVisuals>();
            if (visuals != null)
            {
                visuals.Hide(kindId);
            }

            Debug.Log(LogTag + " end target=" + Damageable.Id + " kind=" + kindId, this);
            OnKindEnded(kindId);
            Ended?.Invoke(kindId);
        }
    }
}

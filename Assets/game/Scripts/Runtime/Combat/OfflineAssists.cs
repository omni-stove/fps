using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    public sealed class OfflineAssists
    {
        private readonly DamageLedger _ledger;
        private readonly Func<int, int, bool> _areAllies;
        private readonly Dictionary<Damageable, int> _ids = new Dictionary<Damageable, int>();
        private readonly Dictionary<int, int> _idsByOfflineId = new Dictionary<int, int>();

        public OfflineAssists(AssistSpec spec, Func<int, int, bool> areAllies)
        {
            _ledger = new DamageLedger(spec ?? throw new ArgumentNullException(nameof(spec)));
            _areAllies = areAllies ?? throw new ArgumentNullException(nameof(areAllies));
        }

        public AssistSpec Spec => _ledger.Spec;

        public double Clock { get; private set; }

        public void Watch(Damageable damageable, int id)
        {
            if (damageable == null)
            {
                throw new ArgumentNullException(nameof(damageable));
            }

            if (_ids.ContainsKey(damageable))
            {
                throw new ArgumentException($"'{damageable.name}' is already watched", nameof(damageable));
            }

            if (_ids.ContainsValue(id))
            {
                throw new ArgumentException($"id {id} is already watched", nameof(id));
            }

            _ids.Add(damageable, id);
            _idsByOfflineId[CombatEvents.OfflineId(damageable.gameObject)] = id;
            damageable.Damaged += OnDamaged;
        }

        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                Clock += deltaSeconds;
            }
        }

        public IReadOnlyList<int> TakeAssisters(int victimId, int killerId)
        {
            return _ledger.TakeAssisters(victimId, killerId, Clock);
        }

        public void Clear(int id)
        {
            _ledger.Clear(id);
        }

        public void Stop()
        {
            foreach (var damageable in _ids.Keys)
            {
                if (damageable != null)
                {
                    damageable.Damaged -= OnDamaged;
                }
            }

            _ids.Clear();
            _idsByOfflineId.Clear();
            _ledger.Reset();
        }

        public void Record(int victimId, int attackerObjectId, int amount)
        {
            if (!_idsByOfflineId.TryGetValue(attackerObjectId, out var attackerId))
            {
                return;
            }

            _ledger.Record(victimId, attackerId, amount, _areAllies(attackerId, victimId), Clock);
        }

        // とどめの一撃は Died の後に届くので、倒れた後のダメージを次の命の記録に残さないよう飛ばす。
        private void OnDamaged(Damageable damageable, DamageReport report)
        {
            if (damageable == null || damageable.IsDead || !_ids.TryGetValue(damageable, out var victimId))
            {
                return;
            }

            var attribution = CombatEvents.CurrentAttribution;
            if (!attribution.HasValue)
            {
                return;
            }

            Record(victimId, attribution.Value.KillerId, report.Amount);
        }
    }
}

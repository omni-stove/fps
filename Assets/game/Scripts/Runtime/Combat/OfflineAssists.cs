using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Runtime
{
    /// <summary>
    /// オフラインのモードで、見張っている <see cref="Damageable"/> が受けたダメージを、そのときの <see cref="CombatEvents.CurrentAttribution"/> の撃ち手と合わせて
    /// <see cref="DamageLedger"/> に記録し、倒されたときにアシストした Id を返す。Id は見張るときに呼び出し側が振ったもの。
    /// 撃ち手が見張っていない物 (帰属の無い落下や環境、モードの外の物) のダメージは記録しない。時刻は <see cref="Advance"/> で進めた秒数。
    /// </summary>
    public sealed class OfflineAssists
    {
        private readonly DamageLedger _ledger;
        private readonly Func<int, int, bool> _areAllies;
        private readonly Dictionary<Damageable, int> _ids = new Dictionary<Damageable, int>();
        private readonly Dictionary<int, int> _idsByOfflineId = new Dictionary<int, int>();

        /// <param name="spec">アシストの調整値。</param>
        /// <param name="areAllies">(撃った Id, 受けた Id) が味方どうしなら true。味方からのダメージはアシストに数えない。</param>
        /// <exception cref="ArgumentNullException">spec か areAllies が null。</exception>
        public OfflineAssists(AssistSpec spec, Func<int, int, bool> areAllies)
        {
            _ledger = new DamageLedger(spec ?? throw new ArgumentNullException(nameof(spec)));
            _areAllies = areAllies ?? throw new ArgumentNullException(nameof(areAllies));
        }

        public AssistSpec Spec => _ledger.Spec;

        /// <summary><see cref="Advance"/> で進めた秒数の合計。</summary>
        public double Clock { get; private set; }

        /// <summary>damageable が受けるダメージを id のものとして記録し始める。</summary>
        /// <exception cref="ArgumentNullException">damageable が null。</exception>
        /// <exception cref="ArgumentException">damageable か id をもう見張っている。</exception>
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

        /// <summary>時刻を deltaSeconds 進める。負・NaN・無限の秒数は進めない。</summary>
        public void Advance(float deltaSeconds)
        {
            if (deltaSeconds > 0f && !float.IsInfinity(deltaSeconds))
            {
                Clock += deltaSeconds;
            }
        }

        /// <summary>
        /// victimId が killerId に今倒されたとして、アシストした Id を <see cref="DamageLedger.TakeAssisters"/> の並びで返し、victimId の記録を消す。
        /// </summary>
        public IReadOnlyList<int> TakeAssisters(int victimId, int killerId)
        {
            return _ledger.TakeAssisters(victimId, killerId, Clock);
        }

        /// <summary>id が受けた記録を消す。出現し直したときに呼ぶ。</summary>
        public void Clear(int id)
        {
            _ledger.Clear(id);
        }

        /// <summary>すべての購読と記録をやめる。</summary>
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

        /// <summary>
        /// attackerObjectId (<see cref="CombatEvents.OfflineId"/>) の撃ち手が victimId に amount を与えたとして記録する。
        /// 撃ち手を見張っていなければ何もしない。<see cref="Damageable.Damaged"/> を通さずに試すときにも使う。
        /// </summary>
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

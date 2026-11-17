using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // オフラインとサーバーの両方で使うので、自分では時計を持たず、呼び出し側の時刻 (秒) をそのまま受ける。
    public sealed class DamageLedger
    {
        private static readonly int[] _none = Array.Empty<int>();

        private readonly Dictionary<int, List<Hit>> _hitsByVictim = new Dictionary<int, List<Hit>>();

        public AssistSpec Spec { get; }

        public DamageLedger(AssistSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public void Record(int victimId, int attackerId, int amount, bool friendly, double time)
        {
            RequireFinite(time);
            if (attackerId == victimId || friendly || amount <= 0)
            {
                return;
            }

            if (!_hitsByVictim.TryGetValue(victimId, out var hits))
            {
                hits = new List<Hit>();
                _hitsByVictim.Add(victimId, hits);
            }

            // 倒されずに長く生き残る相手でも記録が増え続けないよう、数えなくなった分をここで捨てる。
            hits.RemoveAll(h => !Spec.InWindow(h.Time, time));
            hits.Add(new Hit(attackerId, amount, time));
        }

        public IReadOnlyList<int> TakeAssisters(int victimId, int killerId, double time)
        {
            RequireFinite(time);
            if (!_hitsByVictim.TryGetValue(victimId, out var hits))
            {
                return _none;
            }

            _hitsByVictim.Remove(victimId);
            var totals = new List<Total>();
            foreach (var hit in hits)
            {
                if (hit.AttackerId == killerId || !Spec.InWindow(hit.Time, time))
                {
                    continue;
                }

                var index = totals.FindIndex(t => t.AttackerId == hit.AttackerId);
                if (index < 0)
                {
                    totals.Add(new Total(hit.AttackerId, hit.Amount, totals.Count));
                }
                else
                {
                    totals[index] = totals[index].Plus(hit.Amount);
                }
            }

            totals.RemoveAll(t => t.Amount < Spec.MinDamage);
            if (totals.Count == 0)
            {
                return _none;
            }

            totals.Sort((a, b) => a.Amount != b.Amount ? b.Amount.CompareTo(a.Amount) : a.Order.CompareTo(b.Order));
            var ids = new int[totals.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = totals[i].AttackerId;
            }

            return ids;
        }

        public void Clear(int victimId)
        {
            _hitsByVictim.Remove(victimId);
        }

        public void Reset()
        {
            _hitsByVictim.Clear();
        }

        private static void RequireFinite(double time)
        {
            if (double.IsNaN(time) || double.IsInfinity(time))
            {
                throw new ArgumentOutOfRangeException(nameof(time), time, "time must be finite");
            }
        }

        private readonly struct Hit
        {
            public Hit(int attackerId, int amount, double time)
            {
                AttackerId = attackerId;
                Amount = amount;
                Time = time;
            }

            public int AttackerId { get; }
            public int Amount { get; }
            public double Time { get; }
        }

        private readonly struct Total
        {
            public Total(int attackerId, long amount, int order)
            {
                AttackerId = attackerId;
                Amount = amount;
                Order = order;
            }

            public int AttackerId { get; }

            // 大きな量を何度も足しても溢れないよう long で持つ。
            public long Amount { get; }
            public int Order { get; }

            public Total Plus(int amount)
            {
                return new Total(AttackerId, Amount + amount, Order);
            }
        }
    }
}

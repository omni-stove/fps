using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    /// <summary>
    /// 誰が誰にいつどれだけ与えたかを、倒された側の Id ごとに持ち、倒されたときにアシストした Id を返す。
    /// オフラインとサーバーの両方で使い、時刻は呼び出し側の時計 (秒) をそのまま受ける。
    /// </summary>
    public sealed class DamageLedger
    {
        private static readonly int[] _none = Array.Empty<int>();

        private readonly Dictionary<int, List<Hit>> _hitsByVictim = new Dictionary<int, List<Hit>>();

        public AssistSpec Spec { get; }

        /// <exception cref="ArgumentNullException">spec が null。</exception>
        public DamageLedger(AssistSpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        /// <summary>
        /// attackerId が victimId に amount を与えたことを記録する。
        /// 自分へのダメージ・味方からのダメージ・0 以下の量は、アシストにならないので記録しない。
        /// </summary>
        /// <param name="friendly">与えた側と受けた側が味方どうしか。</param>
        /// <param name="time">与えた時刻 (秒)。</param>
        /// <exception cref="ArgumentOutOfRangeException">time が有限でない。</exception>
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

        /// <summary>
        /// victimId が time に killerId に倒されたとして、killerId を除き、<see cref="AssistSpec.WindowSeconds"/> 秒のうちに
        /// 合わせて <see cref="AssistSpec.MinDamage"/> 以上を与えた Id を返し、victimId の記録を消す。
        /// 並びは与えた量の多い順で、同じ量なら先に与えた順。いなければ空。
        /// </summary>
        /// <param name="killerId">倒した Id。自滅や武器によらない死なら victimId と同じでよい。</param>
        /// <exception cref="ArgumentOutOfRangeException">time が有限でない。</exception>
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

        /// <summary>victimId が受けた記録を消す。復活したときに呼ぶ。</summary>
        public void Clear(int victimId)
        {
            _hitsByVictim.Remove(victimId);
        }

        /// <summary>すべての記録を消す。試合やラウンドの始めに呼ぶ。</summary>
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

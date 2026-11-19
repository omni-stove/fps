using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    // 人ごとに使った回数と戻るまでの待ちを持つ。分隊が残っているかは知らないので、BattleRoyaleSession が見てから Begin を呼ぶ。
    // 回数は待ちを始めたときに減らす。待ちの間に分隊が全滅しても、その分隊はもう戻らないので返さない。
    public sealed class Redeploy
    {
        private readonly Dictionary<int, int> _used = new Dictionary<int, int>();
        private readonly Dictionary<int, Waiter> _waiters = new Dictionary<int, Waiter>();

        public Redeploy(RedeploySpec spec)
        {
            Spec = spec ?? throw new ArgumentNullException(nameof(spec));
        }

        public RedeploySpec Spec { get; }

        public int PhaseIndex { get; private set; }

        public bool IsOpen => Spec.IsEnabled && PhaseIndex <= Spec.LastPhaseIndex;

        public int WaitingCount => _waiters.Count;

        // 縮むエリアを止めて状態が読めなくなったときに前の段階の値が来ても、一度閉じた戻り口を開き直さないよう、大きい方だけを取る。
        public void SetPhase(int phaseIndex)
        {
            if (phaseIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(phaseIndex), phaseIndex, "phaseIndex must not be negative");
            }

            PhaseIndex = Math.Max(PhaseIndex, phaseIndex);
        }

        public int ChargesLeft(int playerId)
        {
            return _used.TryGetValue(playerId, out var used) ? Math.Max(0, Spec.Charges - used) : Spec.Charges;
        }

        public RedeployVerdict Check(int playerId)
        {
            if (!Spec.IsEnabled)
            {
                return RedeployVerdict.Disabled;
            }

            if (ChargesLeft(playerId) == 0)
            {
                return RedeployVerdict.NoCharges;
            }

            return PhaseIndex > Spec.LastPhaseIndex ? RedeployVerdict.PastPhase : RedeployVerdict.Waiting;
        }

        // 待ちを始めたら Waiting を返す。待ちが 0 秒なら、その場で IsReady になる。
        public RedeployVerdict Begin(int playerId)
        {
            var verdict = Check(playerId);
            if (verdict != RedeployVerdict.Waiting)
            {
                _waiters.Remove(playerId);
                return verdict;
            }

            _used[playerId] = (_used.TryGetValue(playerId, out var used) ? used : 0) + 1;
            _waiters[playerId] = new Waiter(Spec.DelaySeconds);
            return verdict;
        }

        public bool IsWaiting(int playerId) => _waiters.ContainsKey(playerId);

        public bool IsReady(int playerId) => _waiters.TryGetValue(playerId, out var waiter) && waiter.Remaining <= 0f;

        public float? GetRemaining(int playerId)
        {
            return _waiters.TryGetValue(playerId, out var waiter) ? Math.Max(0f, waiter.Remaining) : (float?)null;
        }

        // 待ちを終えた人を、長く待った順 (同じなら playerId 順) に返す。待ちから外すのは Land か Cancel で行う。
        public IReadOnlyList<int> Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            List<KeyValuePair<int, Waiter>> ready = null;
            foreach (var pair in _waiters)
            {
                pair.Value.Remaining -= deltaSeconds;
                if (pair.Value.Remaining <= 0f)
                {
                    (ready ??= new List<KeyValuePair<int, Waiter>>()).Add(pair);
                }
            }

            if (ready == null)
            {
                return Array.Empty<int>();
            }

            ready.Sort((a, b) =>
            {
                var byTime = a.Value.Remaining.CompareTo(b.Value.Remaining);
                return byTime != 0 ? byTime : a.Key.CompareTo(b.Key);
            });

            var ids = new int[ready.Count];
            for (var i = 0; i < ids.Length; i++)
            {
                ids[i] = ready[i].Key;
            }

            return ids;
        }

        public bool Land(int playerId)
        {
            if (!IsReady(playerId))
            {
                return false;
            }

            _waiters.Remove(playerId);
            return true;
        }

        public void Cancel(int playerId) => _waiters.Remove(playerId);

        public void CancelAll() => _waiters.Clear();

        // 次の試合では回数を戻し、段階も最初からにする。
        public void Reset()
        {
            _waiters.Clear();
            _used.Clear();
            PhaseIndex = 0;
        }

        private sealed class Waiter
        {
            public Waiter(float remaining)
            {
                Remaining = remaining;
            }

            public float Remaining { get; set; }
        }
    }
}

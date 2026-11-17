using System;
using System.Collections.Generic;
using System.Numerics;

namespace Shooter.Core
{
    public sealed class LagCompensator
    {
        // 60Hz で約 200ms までにしてある。
        public const int DefaultMaxRewindTicks = 12;

        private readonly Dictionary<int, HitboxHistory> _histories = new Dictionary<int, HitboxHistory>();
        private readonly Dictionary<int, StanceHistory> _stances = new Dictionary<int, StanceHistory>();

        public int HistoryCapacity { get; }

        public int MaxRewindTicks { get; }

        public LagCompensator(int historyCapacity = HitboxHistory.DefaultCapacity, int maxRewindTicks = DefaultMaxRewindTicks)
        {
            if (historyCapacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(historyCapacity), historyCapacity, "historyCapacity must be positive");
            }

            if (maxRewindTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxRewindTicks), maxRewindTicks, "maxRewindTicks must not be negative");
            }

            HistoryCapacity = historyCapacity;
            MaxRewindTicks = maxRewindTicks;
        }

        public void Record(int tick, int entityId, Vector3 position)
        {
            Record(tick, entityId, position, Vector3.Zero);
        }

        private void Record(int tick, int entityId, Vector3 position, Vector3 lean)
        {
            if (!_histories.TryGetValue(entityId, out var history))
            {
                history = new HitboxHistory(HistoryCapacity);
                _histories.Add(entityId, history);
            }

            history.Record(tick, position, lean);
        }

        public void Record(int tick, int entityId, Vector3 position, bool crouched)
        {
            Record(tick, entityId, position, crouched, Vector3.Zero);
        }

        /// <summary>
        /// entityId の tick 時点の位置・しゃがみ・リーンの傾き (<see cref="LeanStep.EyeOffset(float, float, MotorSpec)"/> の目の横のずれ) を記録する。
        /// 初めての ID なら履歴を作る。
        /// </summary>
        public void Record(int tick, int entityId, Vector3 position, bool crouched, Vector3 lean)
        {
            // 位置の履歴が古い tick を拒んだときに姿勢だけ記録されないよう、位置を先に記録する
            Record(tick, entityId, position, lean);

            if (!_stances.TryGetValue(entityId, out var stance))
            {
                stance = new StanceHistory(HistoryCapacity);
                _stances.Add(entityId, stance);
            }

            stance.Record(tick, crouched);
        }

        public bool Remove(int entityId)
        {
            var removedStance = _stances.Remove(entityId);
            return _histories.Remove(entityId) || removedStance;
        }

        public IReadOnlyDictionary<int, Vector3> Rewind(float tick)
        {
            var result = new Dictionary<int, Vector3>();
            foreach (var pair in _histories)
            {
                if (pair.Value.TrySample(tick, out var position))
                {
                    result.Add(pair.Key, position);
                }
            }

            return result;
        }

        public IReadOnlyDictionary<int, Vector3> Rewind(float tick, int currentTick)
        {
            return Rewind(ClampTick(tick, currentTick));
        }

        // 巻き戻し幅の上限はかけないので、呼ぶ側は ClampTick で丸めた tick を渡す。
        public IReadOnlyDictionary<int, bool> RewindCrouched(float tick)
        {
            var result = new Dictionary<int, bool>();
            foreach (var pair in _stances)
            {
                if (pair.Value.TrySample(tick, out var crouched))
                {
                    result.Add(pair.Key, crouched);
                }
            }

            return result;
        }

        public IReadOnlyDictionary<int, Vector3> RewindLean(float tick)
        {
            var result = new Dictionary<int, Vector3>();
            foreach (var pair in _histories)
            {
                if (pair.Value.TrySample(tick, out _, out var lean))
                {
                    result.Add(pair.Key, lean);
                }
            }

            return result;
        }

        public float ClampTick(float tick, int currentTick)
        {
            if (float.IsNaN(tick))
            {
                return tick;
            }

            var oldest = (double)currentTick - MaxRewindTicks;
            return (float)Math.Min(Math.Max(tick, oldest), currentTick);
        }
    }
}

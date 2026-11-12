using System;

namespace Fps.Core
{
    /// <summary>
    /// 1 つの目標の状態と進み具合の写し。<see cref="Mission.Snapshot"/> で書き出し、<see cref="Mission.Restore"/> で戻す。不変。
    /// </summary>
    public sealed class ObjectiveProgress
    {
        /// <summary>目標の Id。</summary>
        public string Id { get; }

        /// <summary>目標の状態。</summary>
        public ObjectiveState State { get; }

        /// <summary>目標の進み具合。0 以上。</summary>
        public int Count { get; }

        /// <param name="id">目標の Id。空にはできない。</param>
        /// <param name="state">目標の状態。</param>
        /// <param name="count">目標の進み具合。0 以上。</param>
        public ObjectiveProgress(string id, ObjectiveState state, int count)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            if (id.Length == 0)
            {
                throw new ArgumentException("id must not be empty", nameof(id));
            }

            if (state < ObjectiveState.Locked || state > ObjectiveState.Failed)
            {
                throw new ArgumentOutOfRangeException(nameof(state), state, "state must be a defined ObjectiveState");
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), count, "count must not be negative");
            }

            Id = id;
            State = state;
            Count = count;
        }
    }
}

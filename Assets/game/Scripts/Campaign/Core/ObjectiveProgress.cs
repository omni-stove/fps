using System;

namespace Shooter.Core
{
    public sealed class ObjectiveProgress
    {
        public string Id { get; }

        public ObjectiveState State { get; }

        public int Count { get; }

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

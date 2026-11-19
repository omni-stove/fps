using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class IdleTracker
    {
        private readonly Dictionary<int, Entry> _entries = new Dictionary<int, Entry>();

        public float KickSeconds { get; }

        public float WarningSeconds { get; }

        public int Count => _entries.Count;

        public IEnumerable<int> PlayerIds => _entries.Keys;

        public IdleTracker(float kickSeconds, float warningSeconds)
        {
            if (!(kickSeconds > 0f) || float.IsInfinity(kickSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(kickSeconds), kickSeconds, "kickSeconds must be positive and finite");
            }

            if (!(warningSeconds > 0f) || float.IsInfinity(warningSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(warningSeconds), warningSeconds, "warningSeconds must be positive and finite");
            }

            KickSeconds = kickSeconds;
            WarningSeconds = warningSeconds;
        }

        public void Add(int playerId)
        {
            if (_entries.ContainsKey(playerId))
            {
                throw new ArgumentException("player " + playerId + " is already tracked", nameof(playerId));
            }

            _entries.Add(playerId, new Entry(KickSeconds));
        }

        public bool Remove(int playerId)
        {
            return _entries.Remove(playerId);
        }

        public bool Contains(int playerId)
        {
            return _entries.ContainsKey(playerId);
        }

        public IdleChange Touch(int playerId)
        {
            if (!_entries.TryGetValue(playerId, out var entry) || entry.Kicked)
            {
                return IdleChange.None;
            }

            entry.Remaining = KickSeconds;
            if (!entry.Warned)
            {
                return IdleChange.None;
            }

            entry.Warned = false;
            return IdleChange.WarningCleared;
        }

        public IdleChange SetCounting(int playerId, bool counting)
        {
            if (!_entries.TryGetValue(playerId, out var entry) || entry.Kicked || entry.Counting == counting)
            {
                return IdleChange.None;
            }

            // やめるときに残りを戻さないのは、倒されるたびに時計が戻ると、放置して撃たれ続ける人がいつまでも残るため。
            entry.Counting = counting;
            if (!counting)
            {
                if (!entry.Warned)
                {
                    return IdleChange.None;
                }

                entry.Warned = false;
                return IdleChange.WarningCleared;
            }

            if (entry.Remaining > WarningSeconds)
            {
                return IdleChange.None;
            }

            entry.Warned = true;
            return IdleChange.Warned;
        }

        public IdleChange Advance(int playerId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be non-negative and finite");
            }

            if (!_entries.TryGetValue(playerId, out var entry) || entry.Kicked || !entry.Counting)
            {
                return IdleChange.None;
            }

            entry.Remaining = Math.Max(0f, entry.Remaining - deltaSeconds);
            if (entry.Remaining <= 0f)
            {
                entry.Kicked = true;
                entry.Warned = false;
                return IdleChange.Kicked;
            }

            if (entry.Warned || entry.Remaining > WarningSeconds)
            {
                return IdleChange.None;
            }

            entry.Warned = true;
            return IdleChange.Warned;
        }

        public float? GetRemaining(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) ? entry.Remaining : (float?)null;
        }

        public bool IsWarned(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) && entry.Warned;
        }

        public bool IsCounting(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) && entry.Counting && !entry.Kicked;
        }

        public bool IsKicked(int playerId)
        {
            return _entries.TryGetValue(playerId, out var entry) && entry.Kicked;
        }

        private sealed class Entry
        {
            public Entry(float remaining)
            {
                Remaining = remaining;
                Counting = true;
            }

            public float Remaining { get; set; }

            public bool Counting { get; set; }

            public bool Warned { get; set; }

            public bool Kicked { get; set; }
        }
    }
}

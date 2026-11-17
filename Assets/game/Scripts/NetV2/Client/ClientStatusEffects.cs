using System;
using System.Collections.Generic;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class ClientStatusEffects
    {
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _ending = new List<Entry>();

        public event Action<int, string> Started;

        public event Action<int, string> Ended;

        public int Count => _entries.Count;

        public void Receive(StatusEffectMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (string.IsNullOrEmpty(message.KindId))
            {
                return;
            }

            var index = IndexOf(message.TargetId, message.KindId);
            if (message.RemainingSeconds > 0f)
            {
                if (index >= 0)
                {
                    _entries[index] = new Entry(message.TargetId, message.KindId, message.RemainingSeconds);
                    return;
                }

                _entries.Add(new Entry(message.TargetId, message.KindId, message.RemainingSeconds));
                Started?.Invoke(message.TargetId, message.KindId);
            }
            else if (index >= 0)
            {
                var entry = _entries[index];
                _entries.RemoveAt(index);
                Ended?.Invoke(entry.TargetId, entry.KindId);
            }
        }

        // サーバーの切れた知らせ (残り 0) を待たずに数え下ろすのは、知らせが遅れても見た目を時間どおりに消すため。
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be a non-negative finite number");
            }

            _ending.Clear();
            for (var i = 0; i < _entries.Count; i++)
            {
                var entry = _entries[i];
                var remaining = entry.RemainingSeconds - deltaSeconds;
                if (remaining > 0f)
                {
                    _entries[i] = new Entry(entry.TargetId, entry.KindId, remaining);
                    continue;
                }

                _entries.RemoveAt(i);
                i--;
                _ending.Add(entry);
            }

            RaiseEnded();
        }

        public float RemainingSeconds(int targetId, string kindId)
        {
            var index = IndexOf(targetId, kindId);
            return index >= 0 ? _entries[index].RemainingSeconds : 0f;
        }

        public bool TryGetLongest(int targetId, out string kindId, out float remainingSeconds)
        {
            kindId = null;
            remainingSeconds = 0f;
            foreach (var entry in _entries)
            {
                if (entry.TargetId == targetId && entry.RemainingSeconds > remainingSeconds)
                {
                    kindId = entry.KindId;
                    remainingSeconds = entry.RemainingSeconds;
                }
            }

            return kindId != null;
        }

        public void Forget(int targetId)
        {
            _ending.Clear();
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].TargetId == targetId)
                {
                    _ending.Add(_entries[i]);
                    _entries.RemoveAt(i);
                    i--;
                }
            }

            RaiseEnded();
        }

        public void Clear()
        {
            _ending.Clear();
            _ending.AddRange(_entries);
            _entries.Clear();
            RaiseEnded();
        }

        private void RaiseEnded()
        {
            foreach (var entry in _ending)
            {
                Ended?.Invoke(entry.TargetId, entry.KindId);
            }

            _ending.Clear();
        }

        private int IndexOf(int targetId, string kindId)
        {
            for (var i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].TargetId == targetId && string.Equals(_entries[i].KindId, kindId, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }

        private readonly struct Entry
        {
            public Entry(int targetId, string kindId, float remainingSeconds)
            {
                TargetId = targetId;
                KindId = kindId;
                RemainingSeconds = remainingSeconds;
            }

            public int TargetId { get; }

            public string KindId { get; }

            public float RemainingSeconds { get; }
        }
    }
}

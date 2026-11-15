using System;
using System.Collections.Generic;
using Fps.Protocol;

namespace Fps.NetV2
{
    /// <summary>
    /// サーバーから届いた状態異常 (<see cref="StatusEffectMessage"/>) を相手と種類ごとに持ち、残りを数え下ろす。
    /// 付いた・切れたを <see cref="Started"/>・<see cref="Ended"/> で知らせ、Unity の側が燃えている見た目を出し消しする。
    /// 自分の分は HUD が <see cref="TryGetLongest"/> で読んで残りを出す。
    /// </summary>
    public sealed class ClientStatusEffects
    {
        private readonly List<Entry> _entries = new List<Entry>();
        private readonly List<Entry> _ending = new List<Entry>();

        /// <summary>相手 (プレイヤー Id) に種類 (Id) の状態異常が付いた。延びただけのときは呼ばない。</summary>
        public event Action<int, string> Started;

        /// <summary>相手 (プレイヤー Id) の種類 (Id) の状態異常が切れた。</summary>
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

        /// <summary>
        /// deltaSeconds 数え下ろし、0 になったものを切れたことにする。サーバーの切れた知らせ (残り 0) より先に切れても、見た目は時間どおりに消える。
        /// </summary>
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

        /// <summary>targetId にかかっている kindId の残り秒数。かかっていなければ 0。</summary>
        public float RemainingSeconds(int targetId, string kindId)
        {
            var index = IndexOf(targetId, kindId);
            return index >= 0 ? _entries[index].RemainingSeconds : 0f;
        }

        /// <summary>targetId にかかっている中で、いちばん長く残る種類と残り秒数。何もかかっていなければ false。</summary>
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

        /// <summary>targetId の状態異常をすべて切れたことにする (相手が見えなくなった)。</summary>
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

        /// <summary>すべて切れたことにする (試合から抜けた)。</summary>
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

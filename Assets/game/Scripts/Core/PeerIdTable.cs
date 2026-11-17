using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class PeerIdTable
    {
        private readonly Dictionary<string, ulong> _idsByKey = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, string> _keysById = new Dictionary<ulong, string>();

        // 外しても Clear しても戻さないのは、一度使った ID を再利用すると、切断直後の古いメッセージを別人のものと取り違えるため。
        private ulong _nextId;

        public PeerIdTable(ulong firstId)
        {
            _nextId = firstId;
        }

        public int Count => _idsByKey.Count;

        public IEnumerable<ulong> Ids => _keysById.Keys;

        public ulong GetOrAdd(string key, out bool added)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("must not be null or empty", nameof(key));
            }

            if (_idsByKey.TryGetValue(key, out var existing))
            {
                added = false;
                return existing;
            }

            if (_nextId == ulong.MaxValue)
            {
                throw new InvalidOperationException("peer ids are exhausted");
            }

            var id = _nextId++;
            _idsByKey.Add(key, id);
            _keysById.Add(id, key);
            added = true;
            return id;
        }

        public bool TryGetId(string key, out ulong id)
        {
            if (key == null)
            {
                id = 0;
                return false;
            }

            return _idsByKey.TryGetValue(key, out id);
        }

        public bool TryGetKey(ulong id, out string key)
        {
            return _keysById.TryGetValue(id, out key);
        }

        public bool Remove(ulong id)
        {
            if (!_keysById.TryGetValue(id, out var key))
            {
                return false;
            }

            _keysById.Remove(id);
            _idsByKey.Remove(key);
            return true;
        }

        public void Clear()
        {
            _idsByKey.Clear();
            _keysById.Clear();
        }
    }
}

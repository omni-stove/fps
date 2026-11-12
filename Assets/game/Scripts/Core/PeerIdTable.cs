using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// 相手を表す文字列 (EOS の ProductUserId など) と、transport が Netcode に渡す数値 ID を対応づける。
    /// 一度使った ID は外しても再利用しない (切断直後の古いメッセージを別人のものと取り違えないため)。
    /// </summary>
    public sealed class PeerIdTable
    {
        private readonly Dictionary<string, ulong> _idsByKey = new Dictionary<string, ulong>(StringComparer.Ordinal);
        private readonly Dictionary<ulong, string> _keysById = new Dictionary<ulong, string>();
        private ulong _nextId;

        /// <param name="firstId">最初に割り当てる ID。それより小さい ID (サーバー自身の ID など) は割り当てない。</param>
        public PeerIdTable(ulong firstId)
        {
            _nextId = firstId;
        }

        public int Count => _idsByKey.Count;

        /// <summary>登録中の ID。列挙中に表を変えないこと。</summary>
        public IEnumerable<ulong> Ids => _keysById.Keys;

        /// <summary>登録済みならその ID を、無ければ新しい ID を割り当てて返す。</summary>
        /// <param name="key">相手を表す空でない文字列。</param>
        /// <param name="added">新しく割り当てたら true。</param>
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

        /// <returns>登録されていて外したら true。</returns>
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

        /// <summary>すべて外す。次に割り当てる ID は戻さない。</summary>
        public void Clear()
        {
            _idsByKey.Clear();
            _keysById.Clear();
        }
    }
}

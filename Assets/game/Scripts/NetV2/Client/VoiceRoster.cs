using System;
using System.Collections.Generic;

namespace Shooter.NetV2
{
    /// <summary>
    /// ボイスの参加者 (EOS の ProductUserId) が、試合のどのプレイヤー (<see cref="Shooter.Protocol.PlayerSnapshot.PlayerId"/>) かの対応。
    /// <see cref="ClientVoice"/> はここに載っていて味方だと分かる人の声だけを受ける。ProductUserId の大文字・小文字は区別しない。
    /// </summary>
    public sealed class VoiceRoster
    {
        private readonly Dictionary<string, int> _players = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        /// <summary>対応が増えた・変わった・消えた。</summary>
        public event Action Changed;

        /// <summary>載っている ProductUserId。</summary>
        public IEnumerable<string> ProductUserIds => _players.Keys;

        public int Count => _players.Count;

        /// <summary><paramref name="productUserId"/> をプレイヤー <paramref name="playerId"/> にする。同じなら何もしない。</summary>
        /// <exception cref="ArgumentException">productUserId が空。</exception>
        /// <exception cref="ArgumentOutOfRangeException">playerId が負。</exception>
        public void Set(string productUserId, int playerId)
        {
            if (string.IsNullOrEmpty(productUserId))
            {
                throw new ArgumentException("a product user id is required", nameof(productUserId));
            }

            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "a player id must not be negative");
            }

            if (_players.TryGetValue(productUserId, out var current) && current == playerId)
            {
                return;
            }

            _players[productUserId] = playerId;
            Changed?.Invoke();
        }

        /// <summary><paramref name="productUserId"/> の対応を消す。無ければ何もしない。</summary>
        public void Remove(string productUserId)
        {
            if (!string.IsNullOrEmpty(productUserId) && _players.Remove(productUserId))
            {
                Changed?.Invoke();
            }
        }

        public void Clear()
        {
            if (_players.Count == 0)
            {
                return;
            }

            _players.Clear();
            Changed?.Invoke();
        }

        public bool TryGetPlayerId(string productUserId, out int playerId)
        {
            playerId = 0;
            return !string.IsNullOrEmpty(productUserId) && _players.TryGetValue(productUserId, out playerId);
        }
    }
}

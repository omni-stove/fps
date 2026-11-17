using System;
using System.Collections.Generic;

namespace Shooter.NetV2
{
    public sealed class VoiceRoster
    {
        private readonly Dictionary<string, int> _players = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public event Action Changed;

        public IEnumerable<string> ProductUserIds => _players.Keys;

        public int Count => _players.Count;

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

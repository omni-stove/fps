using System;
using System.Collections.Generic;
using Shooter.Protocol;

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

        // サーバーはいつも全員分を送るので、届いた一覧で置き換え、変わったときだけ 1 度知らせる。
        // VoiceRosterMessage は同じ Id を 2 度載せたものを読まずに断るので、数と中身が同じなら置き換えても同じになる。
        public void Apply(VoiceRosterMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (Same(message))
            {
                return;
            }

            _players.Clear();
            for (var i = 0; i < message.Count; i++)
            {
                _players[message.ProductUserId(i)] = message.PlayerId(i);
            }

            Changed?.Invoke();
        }

        public bool TryGetPlayerId(string productUserId, out int playerId)
        {
            playerId = 0;
            return !string.IsNullOrEmpty(productUserId) && _players.TryGetValue(productUserId, out playerId);
        }

        private bool Same(VoiceRosterMessage message)
        {
            if (message.Count != _players.Count)
            {
                return false;
            }

            for (var i = 0; i < message.Count; i++)
            {
                if (!_players.TryGetValue(message.ProductUserId(i), out var playerId) || playerId != message.PlayerId(i))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

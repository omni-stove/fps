using System;
using System.Collections.Generic;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class VoiceRoster
    {
        private readonly Dictionary<string, int> _players = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _verified = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public event Action Changed;

        public IEnumerable<string> ProductUserIds => _players.Keys;

        public int Count => _players.Count;

        public void Set(string productUserId, int playerId, bool verified)
        {
            if (string.IsNullOrEmpty(productUserId))
            {
                throw new ArgumentException("a product user id is required", nameof(productUserId));
            }

            if (playerId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(playerId), playerId, "a player id must not be negative");
            }

            if (_players.TryGetValue(productUserId, out var current) && current == playerId && _verified.Contains(productUserId) == verified)
            {
                return;
            }

            _players[productUserId] = playerId;
            SetVerified(productUserId, verified);
            Changed?.Invoke();
        }

        public void Remove(string productUserId)
        {
            if (!string.IsNullOrEmpty(productUserId) && _players.Remove(productUserId))
            {
                _verified.Remove(productUserId);
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
            _verified.Clear();
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
            _verified.Clear();
            for (var i = 0; i < message.Count; i++)
            {
                _players[message.ProductUserId(i)] = message.PlayerId(i);
                SetVerified(message.ProductUserId(i), message.Verified(i));
            }

            Changed?.Invoke();
        }

        public bool TryGetPlayerId(string productUserId, out int playerId)
        {
            playerId = 0;
            return !string.IsNullOrEmpty(productUserId) && _players.TryGetValue(productUserId, out playerId);
        }

        public bool IsVerified(string productUserId)
        {
            return !string.IsNullOrEmpty(productUserId) && _verified.Contains(productUserId);
        }

        private void SetVerified(string productUserId, bool verified)
        {
            if (verified)
            {
                _verified.Add(productUserId);
            }
            else
            {
                _verified.Remove(productUserId);
            }
        }

        private bool Same(VoiceRosterMessage message)
        {
            if (message.Count != _players.Count)
            {
                return false;
            }

            for (var i = 0; i < message.Count; i++)
            {
                if (!_players.TryGetValue(message.ProductUserId(i), out var playerId) || playerId != message.PlayerId(i)
                    || _verified.Contains(message.ProductUserId(i)) != message.Verified(i))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

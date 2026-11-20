using System;
using System.Collections.Generic;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.NetV2
{
    public sealed class ClientPlayerNames
    {
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        public event Action Changed;

        public int Count => _names.Count;

        public bool Handle(IMessage message)
        {
            if (!(message is PlayerNamesMessage names))
            {
                return false;
            }

            Apply(names);
            return true;
        }

        // サーバーはいつも全員分を送るので、届いた名簿で置き換え、変わったときだけ 1 度知らせる。
        // MessageTable は入れ物を使い回すので、届いた名簿は指さずに写しておく。
        public void Apply(PlayerNamesMessage message)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            if (Same(message))
            {
                return;
            }

            _names.Clear();
            for (var i = 0; i < message.Count; i++)
            {
                _names[message.PlayerId(i)] = message.Name(i);
            }

            Changed?.Invoke();
        }

        public bool TryGetName(int playerId, out string name)
        {
            return _names.TryGetValue(playerId, out name);
        }

        // 名簿より先にスナップショットや撃破が届くことがあり、Bot は名簿に載らないこともあるので、知らない Id は空にせず今までの呼び名を返す。
        public string NameOf(int playerId)
        {
            return _names.TryGetValue(playerId, out var name) ? name : BotRoster.KillFeedName(playerId);
        }

        public void Clear()
        {
            if (_names.Count == 0)
            {
                return;
            }

            _names.Clear();
            Changed?.Invoke();
        }

        // PlayerNamesMessage は同じ Id を 2 度載せたものを読まずに断るので、数と中身が同じなら置き換えても同じになる。
        private bool Same(PlayerNamesMessage message)
        {
            if (message.Count != _names.Count)
            {
                return false;
            }

            for (var i = 0; i < message.Count; i++)
            {
                if (!_names.TryGetValue(message.PlayerId(i), out var name)
                    || !string.Equals(name, message.Name(i), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}

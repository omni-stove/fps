using System;
using System.Collections.Generic;
using System.Globalization;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 名前は入ったときに 1 度だけ決め、ほかの人が出ても付け直さない。
    // 先に入った同名の人が抜けたときに番号を外すと、試合の途中で同じ人の名前が変わって見えるため。
    internal sealed class ServerPlayerNames
    {
        private const int _firstDuplicateNumber = 2;

        private readonly ChatWordFilter _filter;
        private readonly int _maxLength;
        private readonly List<int> _order = new List<int>();
        private readonly Dictionary<int, string> _names = new Dictionary<int, string>();

        // 送るたびに作り直さず使い回すのは、VoiceRosterMessage と同じく出入りのたびの割り当てを避けるため
        private readonly PlayerNamesMessage _message = new PlayerNamesMessage();

        public ServerPlayerNames(ChatWordFilter filter, int maxLength)
        {
            _filter = filter ?? throw new ArgumentNullException(nameof(filter));
            if (maxLength < 1 || maxLength > NetMatchSettings.MaxMaxDisplayNameLength)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maxLength), maxLength, nameof(maxLength) + " must be between 1 and " + NetMatchSettings.MaxMaxDisplayNameLength);
            }

            _maxLength = maxLength;
        }

        public int Count => _order.Count;

        // 返す名簿は次の Join・Leave で中身が変わるので、呼ぶ側はその前に送り終える
        public PlayerNamesMessage Current => _message;

        public bool TryGetName(int playerId, out string name)
        {
            return _names.TryGetValue(playerId, out name);
        }

        // つなぎ直しなどで同じ Id がもう一度入ったときは、並びを保ったまま名前だけを決め直す
        public PlayerNamesMessage Join(ServerPlayer player)
        {
            ArgumentNullException.ThrowIfNull(player);

            var playerId = player.PlayerId;
            if (_names.Remove(playerId))
            {
                _names[playerId] = Distinct(BaseName(player), playerId);
            }
            else
            {
                if (_order.Count == PlayerNamesMessage.MaxEntries)
                {
                    throw new InvalidOperationException("a name roster holds at most " + PlayerNamesMessage.MaxEntries + " players");
                }

                _names[playerId] = Distinct(BaseName(player), playerId);
                _order.Add(playerId);
            }

            return Rebuild();
        }

        // 名簿にいない Id でも断らないのは、名乗る前に切れた人の Leave が来ても全員分を送り直せるようにするため
        public PlayerNamesMessage Leave(int playerId)
        {
            if (_names.Remove(playerId))
            {
                _order.Remove(playerId);
            }

            return Rebuild();
        }

        public void Clear()
        {
            _order.Clear();
            _names.Clear();
            _message.Clear();
        }

        // Bot は名乗らないので、本人の欄を見ずにいつも BotRoster の名前にする。
        // 伏せ字にした名前を出さずに戻すのは、伏せた形でも何の語だったかが周りに読めてしまうため
        private string BaseName(ServerPlayer player)
        {
            var fallback = BotRoster.KillFeedName(player.PlayerId);
            if (player.IsBot)
            {
                return fallback;
            }

            // MatchServer で検査済みでも通し直すのは、ほかの道で作った ServerPlayer の名前もそのまま送らないため
            var name = PlayerDisplayName.Sanitize(player.DisplayName, _maxLength);
            if (name.Length == 0 || _filter.Contains(name))
            {
                return fallback;
            }

            return name;
        }

        private string Distinct(string name, int playerId)
        {
            if (!IsTaken(name))
            {
                return name;
            }

            for (var number = _firstDuplicateNumber; ; number++)
            {
                var numbered = Numbered(name, number);

                // 切ったことで禁止語が語として成り立つことがあるので、番号を付けた名前も通し直す。
                // 戻した先の名前は BotRoster の形なので、通し直さずに番号だけ付ける
                if (_filter.Contains(numbered))
                {
                    return Unfiltered(BotRoster.KillFeedName(playerId));
                }

                if (!IsTaken(numbered))
                {
                    return numbered;
                }
            }
        }

        private string Unfiltered(string name)
        {
            if (!IsTaken(name))
            {
                return name;
            }

            for (var number = _firstDuplicateNumber; ; number++)
            {
                var numbered = Numbered(name, number);
                if (!IsTaken(numbered))
                {
                    return numbered;
                }
            }
        }

        // 番号は設定の長さでなく送れる長さまでに収めるのは、上限いっぱいの名前でも元の名前を削らずに読めるようにするため
        private static string Numbered(string name, int number)
        {
            var suffix = " (" + number.ToString(CultureInfo.InvariantCulture) + ")";
            var room = NetMatchSettings.MaxMaxDisplayNameLength - suffix.Length;
            var head = name;
            if (head.Length > room)
            {
                var cut = room;

                // サロゲートの対の間で切ると UTF-8 にできず、名簿ごと送れなくなるため
                if (cut > 0 && char.IsHighSurrogate(head[cut - 1]))
                {
                    cut--;
                }

                head = head.Substring(0, cut).TrimEnd();
            }

            return head + suffix;
        }

        // 全角と半角、大文字と小文字の違いだけの名前は画面で見分けにくいので、ChatWordFilter と同じそろえ方で同じ名前と見なす
        private bool IsTaken(string name)
        {
            var folded = ChatWordFilter.Fold(name);
            foreach (var taken in _names.Values)
            {
                if (string.Equals(ChatWordFilter.Fold(taken), folded, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private PlayerNamesMessage Rebuild()
        {
            _message.Clear();
            foreach (var playerId in _order)
            {
                _message.Add(playerId, _names[playerId]);
            }

            return _message;
        }
    }
}

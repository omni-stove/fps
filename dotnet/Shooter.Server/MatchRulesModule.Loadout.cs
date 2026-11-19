using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // プレイヤーが選んだ武器の並びを、武器の一覧とロードアウトのルールで検査して次の出現から配る並びとして控える。
    // 控えた並びは次の試合でも残り、退出で消える。今持っている武器は変えない
    internal sealed partial class MatchRulesModule
    {
        private readonly LoadoutAnswerMessage _loadoutAnswer = new LoadoutAnswerMessage();
        private readonly List<string> _requestedWeaponIds = new List<string>();
        private readonly Dictionary<int, string> _cosmetics = new Dictionary<int, string>();

        public string CosmeticOf(int playerId)
        {
            return _cosmetics.TryGetValue(playerId, out var id) ? id : string.Empty;
        }

        // 出現のたびに配る並び。経済で買った武器は、選んだ並び (無ければ既定の並び) の同じ枠を置き換え、倒れて失うまで次の出現でも持つ
        private IReadOnlyList<WeaponSpec> LoadoutFor(int playerId)
        {
            return WithBoughtWeapons(playerId, _loadout.WeaponsFor(playerId));
        }

        // サーバーは見た目のカタログを持たないので Id を確かめずに中継し、知らない Id は受け取った側が既定の見た目にする。
        // 見た目は当たりに関わらないので、解けているかもクライアントの申告をそのまま信じる。
        private void ServerChooseCosmetic(ServerPlayer player, string cosmeticId)
        {
            var id = cosmeticId?.Trim() ?? string.Empty;
            if (string.Equals(id, CosmeticOf(player.PlayerId), StringComparison.Ordinal))
            {
                return;
            }

            if (id.Length == 0)
            {
                _cosmetics.Remove(player.PlayerId);
            }
            else
            {
                _cosmetics[player.PlayerId] = id;
            }

            _log.WriteLine(LoadoutChoiceLog.Cosmetic((ulong)player.PlayerId, id));
        }

        private void ServerChooseLoadout(ServerPlayer player, ContentIdList weaponIds)
        {
            // 武器の無い要求は、並びを保存していない持ち主が見た目だけを送るためのものなので、並びを変えず答えも返さない
            if (weaponIds.Count == 0)
            {
                return;
            }

            _requestedWeaponIds.Clear();
            for (var i = 0; i < weaponIds.Count; i++)
            {
                _requestedWeaponIds.Add(weaponIds[i]);
            }

            var clientId = (ulong)player.PlayerId;
            var accepted = _loadout.TryChoose(player.PlayerId, _requestedWeaponIds, out var reason);
            _log.WriteLine(accepted
                ? LoadoutChoiceLog.Accepted(clientId, _loadout.WeaponsFor(player.PlayerId))
                : LoadoutChoiceLog.Rejected(clientId, reason));

            _loadoutAnswer.Accepted = accepted;
            _loadoutAnswer.Reason = accepted ? string.Empty : Truncate(reason ?? string.Empty, LoadoutAnswerMessage.MaxReasonBytes);
            _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _loadoutAnswer);
        }

        // 理由は武器の Id を並べて長くなることがあるので、送れる長さに文字の途中で切らずに詰める
        internal static string Truncate(string text, int maxBytes)
        {
            if (Encoding.UTF8.GetByteCount(text) <= maxBytes)
            {
                return text;
            }

            var length = text.Length;
            while (length > 0 && Encoding.UTF8.GetByteCount(text.AsSpan(0, length)) > maxBytes)
            {
                length--;
                if (length > 0 && char.IsHighSurrogate(text[length - 1]))
                {
                    length--;
                }
            }

            return text.Substring(0, length);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Text;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // プレイヤーが選んだ武器の並びを、武器の一覧とロードアウトのルールで検査して次の出現から配る並びとして控える。
    // 控えた並びは次の試合でも残り、退出で消える。今持っている武器は変えない。能力の並びは Abilities で検査して控える
    internal sealed partial class MatchRulesModule
    {
        private readonly LoadoutAnswerMessage _loadoutAnswer = new LoadoutAnswerMessage();
        private readonly List<string> _requestedWeaponIds = new List<string>();
        private readonly List<string> _requestedAbilityIds = new List<string>();
        private readonly Dictionary<int, string> _cosmetics = new Dictionary<int, string>();

        // 置ける物のモジュール (ServerDeployables) が作られるときに、置ける物の一覧を持つものに差し替える。
        // 差し替えられていなければ候補が無いので、どの枠も「なし」の選択だけを受け入れる
        internal ServerAbilityChoices Abilities { get; set; } = new ServerAbilityChoices();

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

        // 能力の無い要求 (能力の枠を持たない前の版のクライアント) は武器だけを選び、持たせた能力は変えない
        private void ServerChooseLoadout(ServerPlayer player, ContentIdList weaponIds, ContentIdList abilityIds)
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

            _requestedAbilityIds.Clear();
            for (var i = 0; i < abilityIds.Count; i++)
            {
                _requestedAbilityIds.Add(abilityIds[i]);
            }

            var clientId = (ulong)player.PlayerId;
            var choosesAbilities = _requestedAbilityIds.Count > 0;
            var abilities = Abilities;
            string reason;
            bool accepted;

            // 答えは要求ごとに 1 つしか返せず、持ち主は受け入れられた要求の武器と能力をまとめて保存するので、
            // 能力を断った要求は武器も選ばせずに全部を断る (武器だけ変わって保存されないことを避ける)
            var abilityReason = choosesAbilities ? abilities.Check(_requestedAbilityIds) : null;
            if (abilityReason != null)
            {
                accepted = false;
                reason = abilityReason;
                _log.WriteLine(LoadoutChoiceLog.RejectedAbilities(clientId, reason));
            }
            else
            {
                accepted = _loadout.TryChoose(player.PlayerId, _requestedWeaponIds, out reason);
                _log.WriteLine(accepted
                    ? LoadoutChoiceLog.Accepted(clientId, _loadout.WeaponsFor(player.PlayerId))
                    : LoadoutChoiceLog.Rejected(clientId, reason));
                if (accepted && choosesAbilities)
                {
                    abilities.Choose(player.PlayerId, _requestedAbilityIds);
                    _log.WriteLine(LoadoutChoiceLog.AcceptedAbilities(clientId, abilities.AbilitiesFor(player.PlayerId)));
                }
            }

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

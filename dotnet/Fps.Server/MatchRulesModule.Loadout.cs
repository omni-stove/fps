using System;
using System.Collections.Generic;
using System.Text;
using Fps.Core;
using Fps.Protocol;

namespace Fps.Server
{
    // プレイヤーが選んだ武器の並びを、武器の一覧とロードアウトのルールで検査して次の出現から配る並びとして控える。
    // 控えた並びは次の試合でも残り、退出で消える。今持っている武器は変えない
    internal sealed partial class MatchRulesModule
    {
        private readonly LoadoutAnswerMessage _loadoutAnswer = new LoadoutAnswerMessage();
        private readonly List<string> _requestedWeaponIds = new List<string>();

        private void ServerChooseLoadout(ServerPlayer player, ContentIdList weaponIds)
        {
            _requestedWeaponIds.Clear();
            for (var i = 0; i < weaponIds.Count; i++)
            {
                _requestedWeaponIds.Add(weaponIds[i]);
            }

            var clientId = (ulong)player.PlayerId;
            var reason = "no weapons were requested";
            var accepted = _requestedWeaponIds.Count > 0 && _loadout.TryChoose(player.PlayerId, _requestedWeaponIds, out reason);
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

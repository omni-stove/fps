using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 届け先はクライアントの言う宛先を信じず、サーバーが送った人の今のチームから決める。
    // チームの文を敵に流せると、味方だけの連絡が相手に漏れるため。
    internal sealed partial class MatchRulesModule
    {
        private readonly ChatMessage _chatMessage = new ChatMessage();

        private ChatGate _chatGate;
        private float _nextChatRefusalLogTime;

        /// <summary>
        /// playerId の文を <see cref="ChatGate"/> に通し、通れば全体なら全員に、チームなら同じチームの人にだけ配る。
        /// 断った文は送った本人にも返さない。参加していない人と試合の無いときは理由も出さずに捨てる (印と同じ)。そのときは null。
        /// </summary>
        internal ChatRefusal? ServerChat(int playerId, ChatChannel channel, string text)
        {
            if (_session == null || _chatGate == null || !_session.HasPlayer(playerId))
            {
                return null;
            }

            var refusal = _chatGate.TryAccept(playerId, channel, text, _now, out var line);
            if (refusal != ChatRefusal.None)
            {
                return RefuseChat(playerId, refusal);
            }

            _chatMessage.SenderId = line.SenderId;
            _chatMessage.Channel = line.Channel;
            _chatMessage.Text = line.Text;
            if (line.Channel == ChatChannel.Team)
            {
                SendToTeam(_session.GetTeam(playerId), TransportChannel.Reliable, _chatMessage);
            }
            else
            {
                SendToAll(TransportChannel.Reliable, _chatMessage);
            }

            _log.WriteLine(ChatLog.Accepted(line));
            _match.WriteChat(line);
            return ChatRefusal.None;
        }

        partial void OnChatRequest(ServerPlayer player, ChatRequestMessage request)
        {
            ServerChat(player.PlayerId, request.Channel, request.Text);
        }

        private void PrepareChat(NetMatchSettings settings)
        {
            _chatGate = new ChatGate(settings);
            _log.WriteLine(ChatLog.Gate(_chatGate));
        }

        private void ChatPlayerLeft(int playerId)
        {
            _chatGate?.Remove(playerId);
        }

        // 連打は普段の遊びでも起きるので、印と同じくログを埋めないよう間引く
        private ChatRefusal RefuseChat(int playerId, ChatRefusal reason)
        {
            if (_now >= _nextChatRefusalLogTime)
            {
                _nextChatRefusalLogTime = _now + _rejectWarningInterval;
                _log.WriteLine(ChatLog.Refused(playerId, reason));
                _match.WriteChatRefused(playerId, reason);
            }

            return reason;
        }
    }
}

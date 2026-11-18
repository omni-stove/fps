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
        private ChatWordFilter _chatWordFilter;
        private float _nextChatRefusalLogTime;

        // 参加していない人と試合の無いときに理由も出さずに捨てるのは、印と同じにするため。
        // 語の絞り込みをクライアントでなくここで掛けるのは、手を入れたクライアントでも素通りさせないため。
        internal ChatRefusal? ServerChat(int playerId, Shooter.Protocol.ChatChannel channel, string text)
        {
            if (_session == null || _chatGate == null || !_session.HasPlayer(playerId))
            {
                return null;
            }

            // 門より先に断るのは、ルールで閉じた文を送った時刻として覚えさせないため。
            if (_session.Spec.Rules.TextChat == TextChat.Off)
            {
                return RefuseChat(playerId, ChatRefusal.RuleOff);
            }

            var refusal = _chatGate.TryAccept(playerId, (Shooter.Core.ChatChannel)(int)channel, text, _now, out var accepted);
            if (refusal != ChatRefusal.None)
            {
                return RefuseChat(playerId, refusal);
            }

            refusal = _chatWordFilter.Apply(accepted, out var line);
            if (refusal != ChatRefusal.None)
            {
                return RefuseChat(playerId, refusal);
            }

            _chatMessage.SenderId = line.SenderId;
            _chatMessage.Channel = (Shooter.Protocol.ChatChannel)(int)line.Channel;
            _chatMessage.Text = line.Text;
            if (line.Channel == Shooter.Core.ChatChannel.Team)
            {
                SendToTeam(_session.GetTeam(playerId), TransportChannel.Reliable, _chatMessage);
            }
            else
            {
                SendToAll(TransportChannel.Reliable, _chatMessage);
            }

            if (!string.Equals(line.Text, accepted.Text, System.StringComparison.Ordinal))
            {
                _log.WriteLine(ChatLog.Masked(line));
            }

            _log.WriteLine(ChatLog.Accepted(line));
            _match.WriteChat(line);
            return ChatRefusal.None;
        }

        partial void OnChatRequest(ServerPlayer player, ChatRequestMessage request)
        {
            ServerChat(player.PlayerId, request.Channel, request.Text);
        }

        private void PrepareChat(NetMatchSettings settings, ChatWordFilterSpec words)
        {
            _chatGate = new ChatGate(settings);
            _chatWordFilter = new ChatWordFilter(words);
            _log.WriteLine(ChatLog.Gate(_chatGate));
            _log.WriteLine(ChatLog.WordFilter(_chatWordFilter));
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

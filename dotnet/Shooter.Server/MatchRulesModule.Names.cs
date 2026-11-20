using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // 名前は出入りのたびに全員分を全員へ送る。入ったばかりの人もこの 1 通で、先にいた人と Bot の名前をまとめて受け取る。
    // 禁止語はチャットと同じ ChatWordFilter で見て、ルールを替えても試合の途中で名前が変わらないよう、名簿は Start で 1 度だけ作る
    internal sealed partial class MatchRulesModule
    {
        private ServerPlayerNames _names;

        internal PlayerNamesMessage CurrentPlayerNames => _names?.Current;

        private void PrepareNames(NetMatchSettings settings)
        {
            _names = new ServerPlayerNames(_chatWordFilter, settings.MaxDisplayNameLength);
        }

        // 名簿は次の出入りで中身が変わるので、返ったその場で送り切る
        private void NamesPlayerJoined(ServerPlayer player)
        {
            var playerId = player.PlayerId;

            // 名簿の上限は試合の人数の上限と同じなので普段は届かないが、届いても入った人を断らず、その人だけ Id の名前で見せる
            if (_names.Count == PlayerNamesMessage.MaxEntries && !_names.TryGetName(playerId, out _))
            {
                _log.WriteLine(ServerPlayerNamesLog.Full(playerId, _names.Count));
                _context.Messenger.Send(player.Connection, TransportChannel.Reliable, _names.Current);
                return;
            }

            var roster = _names.Join(player);
            _names.TryGetName(playerId, out var name);
            _log.WriteLine(ServerPlayerNamesLog.Joined(playerId, name, roster.Count));
            SendToAll(TransportChannel.Reliable, roster);
        }

        private void NamesPlayerLeft(ServerPlayer player)
        {
            var playerId = player.PlayerId;
            if (!_names.TryGetName(playerId, out _))
            {
                return;
            }

            var roster = _names.Leave(playerId);
            _log.WriteLine(ServerPlayerNamesLog.Left(playerId, roster.Count));
            SendToAll(TransportChannel.Reliable, roster);
        }

        private void StopNames()
        {
            _names?.Clear();
            _names = null;
        }
    }
}

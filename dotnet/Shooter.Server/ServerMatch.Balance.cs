using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerMatch
    {
        /// <summary>
        /// 次の試合の前に人数をそろえるため、プレイヤーのチームを移した直後 (2 つ目の引数は移す前のチーム)。
        /// 続けて <see cref="Restarted"/> が来る。ルールのモジュールがチームの持ち主 (Session) を合わせる。
        /// </summary>
        public event Action<ServerPlayer, Team> TeamChanged;

        partial void BalanceTeamsForNextMatch()
        {
            if (MaxTeamSizeDifference == 0)
            {
                return;
            }

            var moves = TeamBalancer.Plan(MembersOf(Team.Alpha), MembersOf(Team.Bravo), MaxTeamSizeDifference);
            foreach (var move in moves)
            {
                // 前の人の TeamChanged の受け手が抜いた人は移さない
                if (!_players.TryGetValue(move.PlayerId, out var player))
                {
                    continue;
                }

                player.MoveTo(move.To);
                _log.WriteLine(MatchLog.TeamBalanced(player.PlayerId, move.From, move.To));
                if (_events.Enabled)
                {
                    _events.Write(ServerEventLog.TeamBalanced(_utcNow(), MatchId, player.PlayerId, player.ProductUserId, move.From, move.To));
                }

                TeamChanged?.Invoke(player, move.From);
            }
        }

        // _players は playerId の順で、Bot の Id は人より大きいので、入った順は JoinOrder で並べ直す
        private List<TeamBalanceMember> MembersOf(Team team)
        {
            var players = new List<ServerPlayer>();
            foreach (var player in _players.Values)
            {
                if (player.Team == team)
                {
                    players.Add(player);
                }
            }

            players.Sort((a, b) => a.JoinOrder.CompareTo(b.JoinOrder));
            var members = new List<TeamBalanceMember>(players.Count);
            foreach (var player in players)
            {
                members.Add(new TeamBalanceMember(player.PlayerId, player.PartyId));
            }

            return members;
        }
    }
}

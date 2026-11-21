using System;
using System.Collections.Generic;
using Shooter.Core;

namespace Shooter.Server
{
    internal sealed partial class ServerMatch
    {
        private readonly List<ServerPlayer> _fixedScratch = new List<ServerPlayer>();

        public event Action<ServerPlayer, Team> TeamChanged;

        // ChooseTeam と同じく、目的を作るルールのモジュールが差し込む。引数は Bot か。null を返せばパーティと人数で決める
        public Func<bool, Team?> FixedTeam { get; set; }

        partial void BalanceTeamsForNextMatch()
        {
            // 目的がチームを決める試合 (Horde) で人数をそろえると、人を敵の側へ移してしまう
            if (MoveToFixedTeams())
            {
                return;
            }

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

                Move(player, move.To);
            }
        }

        // 前の試合を別の目的で遊んでいた人を、次の試合の目的が決めるチームへ移す。目的がどのチームも決めなければ false
        private bool MoveToFixedTeams()
        {
            var choose = FixedTeam;
            if (choose == null)
            {
                return false;
            }

            _fixedScratch.Clear();
            _fixedScratch.AddRange(_players.Values);
            var decided = false;
            foreach (var player in _fixedScratch)
            {
                var team = choose(player.IsBot);
                if (!team.HasValue)
                {
                    continue;
                }

                decided = true;
                if (player.Team != team.Value && _players.ContainsKey(player.PlayerId))
                {
                    Move(player, team.Value);
                }
            }

            _fixedScratch.Clear();
            return decided;
        }

        private void Move(ServerPlayer player, Team to)
        {
            var from = player.Team;
            player.MoveTo(to);
            _log.WriteLine(MatchLog.TeamBalanced(player.PlayerId, from, to));
            if (_events.Enabled)
            {
                _events.Write(ServerEventLog.TeamBalanced(_utcNow(), MatchId, player.PlayerId, player.ProductUserId, from, to));
            }

            TeamChanged?.Invoke(player, from);
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

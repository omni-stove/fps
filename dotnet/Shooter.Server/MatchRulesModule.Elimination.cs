using System;
using Shooter.Core;
using Shooter.Protocol;

namespace Shooter.Server
{
    // GameRules.Objective が Elimination のとき、試合をラウンドに区切る。Session (TeamDeathmatchSession) は倒れた人を出直させず、
    // 全滅でも試合を終えない形で作り、ラウンドの勝ち負けは EliminationObjective が決める。勝ち数が上限に届いたら Session を Finish で終える。
    // 試合全体の制限時間は設けず、1 ラウンドの制限時間で区切る
    internal sealed partial class MatchRulesModule
    {
        // ラウンド制の試合でなければ null
        private EliminationObjective _elimination;

        internal EliminationObjective Elimination => _elimination;

        // 合間は撃ち合わせない。撃ち始めの秒を全員でそろえるため
        private bool IsBetweenRounds => _elimination != null && _elimination.IsPreparing;

        private void PrepareElimination(GameRules rules, NetMatchSettings settings)
        {
            _elimination = null;
            if (rules.Objective != MatchObjective.Elimination)
            {
                return;
            }

            try
            {
                _elimination = new EliminationObjective(
                    settings.RoundWinsToWin, settings.RoundPreparationSeconds, settings.RoundTimeLimitSeconds, CountStanding);
            }
            catch (ArgumentException e)
            {
                throw new InvalidOperationException("cannot start the " + rules.Objective + " match: " + e.Message, e);
            }
        }

        private void StartElimination()
        {
            if (_elimination == null)
            {
                return;
            }

            _elimination.PreparationStarted += HandleRoundPreparing;
            _elimination.RoundStarted += HandleRoundLive;
            _elimination.RoundEnded += HandleRoundEnded;
            RoundPhaseSource = EliminationPhase;
            var rounds = _elimination.Rounds;
            _log.WriteLine(MatchLog.Elimination(rounds.WinsToWin, rounds.PreparationSeconds, _elimination.Mode.TimeLimitSeconds));
        }

        private void StopElimination()
        {
            if (_elimination == null)
            {
                return;
            }

            _elimination.PreparationStarted -= HandleRoundPreparing;
            _elimination.RoundStarted -= HandleRoundLive;
            _elimination.RoundEnded -= HandleRoundEnded;
            if (RoundPhaseSource == EliminationPhase)
            {
                RoundPhaseSource = null;
            }

            _elimination = null;
        }

        private void TickElimination(float deltaSeconds)
        {
            if (_elimination == null)
            {
                return;
            }

            if (_elimination.Tick(_session.State == GameModeState.Playing, deltaSeconds))
            {
                // 続く HandleEnded の DecideWinner が同じ勝者を選ぶ
                _session.Finish(_elimination.DecideWinner());
            }
        }

        // 試合が終わった後もラウンドが Live のまま残りうるので、試合の終わりを Ended として送る
        private RoundPhase? EliminationPhase()
        {
            if (_elimination == null)
            {
                return null;
            }

            return _session != null && _session.State == GameModeState.Ended ? RoundPhase.Ended : _elimination.Phase;
        }

        private void EliminationPlayerJoined(int playerId)
        {
            if (_elimination != null && _session.HasPlayer(playerId))
            {
                _elimination.SetPlayer(playerId, _session.GetTeam(playerId));
            }
        }

        private void EliminationPlayerLeft(int playerId)
        {
            _elimination?.RemovePlayer(playerId);
        }

        private void EliminationPlayerKilled(int killerId, int victimId)
        {
            _elimination?.ReportFall(killerId, victimId);
        }

        private void EliminationMatchRestarted()
        {
            if (_elimination == null)
            {
                return;
            }

            _elimination.Reset();
            _match.SetScores(0, 0);
        }

        private void FillElimination(ref MatchSnapshot snapshot)
        {
            if (_elimination == null)
            {
                return;
            }

            snapshot.AlphaPoints = _elimination.GetRoundWins(Team.Alpha);
            snapshot.BravoPoints = _elimination.GetRoundWins(Team.Bravo);
        }

        // ダウン中の人を数えないのは、時間切れの時点で撃ち合えない人だから
        private int CountStanding(Team team)
        {
            var count = 0;
            foreach (var playerId in _session.GetPlayerIds())
            {
                if (_session.GetTeam(playerId) == team && _session.IsAlive(playerId) && !_session.IsDowned(playerId))
                {
                    count++;
                }
            }

            return count;
        }

        // 体力と位置は Session が持つので、ここで全員を出直させる。倒れている人の蘇生の受け付けも捨てる
        private void HandleRoundPreparing(int round)
        {
            _log.WriteLine(MatchLog.RoundPreparing(round));
            ClearDowned();
            _session.RespawnAll();
        }

        private void HandleRoundLive(int round)
        {
            _log.WriteLine(MatchLog.RoundLive(round));
        }

        private void HandleRoundEnded(int round, Team? winner)
        {
            var alpha = _elimination.GetRoundWins(Team.Alpha);
            var bravo = _elimination.GetRoundWins(Team.Bravo);
            _log.WriteLine(MatchLog.RoundEnded(round, winner, alpha, bravo));
            _match.SetScores(alpha, bravo);
        }
    }
}

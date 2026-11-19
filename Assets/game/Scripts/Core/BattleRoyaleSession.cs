using System;
using System.Collections.Generic;

namespace Shooter.Core
{
    public sealed class BattleRoyaleSession : IBattleRoyaleView
    {
        public const int DefaultSquadCount = 4;
        public const int DefaultSquadSize = 2;

        public event Action<int, int?> PlayerDowned;

        public event Action<int, int> PlayerRevived;

        public event Action<int, int?> PlayerBledOut;

        public event Action<int, int?> PlayerFinished;

        public event Action<Side, int> SquadEliminated;

        public event Action<Side?> Ended;

        // 倒れた人ごとに、戻る待ちに入ったか、なぜ戻らないかを知らせる。待ちが 0 秒なら、この後すぐ Mode.RespawnReady が来る。
        public event Action<int, RedeployVerdict> RedeployDecided;

        private readonly ModeSession _mode;

        private readonly DownedTracker _downed;

        private readonly Redeploy _redeploy;

        private readonly Dictionary<Side, int> _placements = new Dictionary<Side, int>();
        private readonly List<Side> _entrants = new List<Side>();

        private readonly Dictionary<Side, long> _finalCounts = new Dictionary<Side, long>();

        public BattleRoyaleSession(int squadCount, int squadSize, float timeLimitSeconds, ActionRules actions, RedeploySpec redeploy = null)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            _redeploy = new Redeploy(redeploy ?? RedeploySpec.None);
            if (actions.Downed == Downed.Revive)
            {
                // ダウン中は撃っても倒れないので、DownedTracker のダウン中の体力は使わない。1 以上でないと作れないので 1 を入れる。
                _downed = new DownedTracker(1, actions.DownedBleedoutSeconds, actions.ReviveSeconds);
            }

            _mode = new ModeSession(
                new Squads(squadCount, squadSize),
                timeLimitSeconds,
                Array.Empty<IScoreRule>(),
                new RedeployPolicy(this),
                new IWinCondition[] { new LastSquadStanding(this), new MostStandingAtTimeUp(this) });
            _mode.Ended += OnModeEnded;
        }

        public ModeSession Mode => _mode;

        public ActionRules Actions { get; }

        public bool UsesDowned => _downed != null;

        // ダウンを別の Session が持つとき、その人がダウン中かを渡す。時間切れの立っている人数から引く
        public Func<int, bool> ExternalDowned { get; set; }

        public bool IsLive => _mode.State == GameModeState.Playing;

        public int EntrantCount => _entrants.Count;

        public int SquadsRemaining => _entrants.Count - _placements.Count;

        public RedeploySpec RedeployRules => _redeploy.Spec;

        public int ZonePhaseIndex => _redeploy.PhaseIndex;

        public bool IsRedeployOpen => _redeploy.IsOpen;

        // 試合の時計と人の並びは Mode から読めるので、公開の口を増やさず HUD に渡す口としてだけ見せる。
        GameModeState IBattleRoyaleView.State => _mode.State;

        bool IBattleRoyaleView.HasTimeLimit => !float.IsPositiveInfinity(_mode.TimeLimitSeconds);

        float IBattleRoyaleView.RemainingSeconds => float.IsPositiveInfinity(_mode.TimeLimitSeconds) ? 0f : _mode.RemainingSeconds;

        double IBattleRoyaleView.ElapsedSeconds => _mode.ElapsedSeconds;

        Side? IBattleRoyaleView.Winner => _mode.Winner;

        IReadOnlyList<Side> IBattleRoyaleView.Sides => _mode.Sides;

        bool IBattleRoyaleView.HasPlayer(int playerId) => _mode.HasPlayer(playerId);

        Side IBattleRoyaleView.GetSide(int playerId) => _mode.GetSide(playerId);

        int IBattleRoyaleView.CountMembers(Side squad) => _mode.CountMembers(squad);

        public void Start()
        {
            _entrants.Clear();
            foreach (var side in _mode.Sides)
            {
                if (_mode.CountMembers(side) > 0)
                {
                    _entrants.Add(side);
                }
            }

            _mode.Start();
        }

        public void Reset()
        {
            _downed?.Clear();
            _placements.Clear();
            _entrants.Clear();
            _finalCounts.Clear();
            _mode.Reset();
        }

        public bool IsDowned(int playerId) => _downed != null && _downed.IsDowned(playerId);

        // 戻れるかは倒されたときの段階で決めるので、待っている間に段階が進んでも待ちは続く。
        public void SetZonePhase(int phaseIndex) => _redeploy.SetPhase(phaseIndex);

        public int GetRedeploysLeft(int playerId)
        {
            RequirePlayer(playerId, nameof(playerId));
            return _redeploy.ChargesLeft(playerId);
        }

        public bool IsAwaitingRedeploy(int playerId) => IsLive && _mode.HasPlayer(playerId) && _mode.IsAwaitingRespawn(playerId);

        public float? GetRedeployRemaining(int playerId) => IsAwaitingRedeploy(playerId) ? _redeploy.GetRemaining(playerId) : null;

        public IReadOnlyList<int> GetDownedIds() => _downed != null ? _downed.GetDownedIds() : (IReadOnlyList<int>)Array.Empty<int>();

        public float? GetBleedoutRemaining(int playerId) => _downed?.GetBleedoutRemaining(playerId);

        public float GetReviveProgress(int playerId) => _downed != null ? _downed.GetReviveProgress(playerId) : 0f;

        public int RevivedHealth(int maxHealth) => DownedTracker.HealthFor(maxHealth, Actions.DownedHealthFraction);

        public bool IsStanding(int playerId) => !_mode.IsDown(playerId) && !IsDowned(playerId);

        public int CountRemaining(Side squad)
        {
            var count = 0;
            foreach (var id in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(id) == squad && !_mode.IsDown(id))
                {
                    count++;
                }
            }

            return count;
        }

        public int CountDowned(Side squad)
        {
            var count = 0;
            foreach (var id in GetDownedIds())
            {
                if (_mode.HasPlayer(id) && _mode.GetSide(id) == squad)
                {
                    count++;
                }
            }

            return count;
        }

        public int CountStanding(Side squad) => CountRemaining(squad) - CountDowned(squad);

        public int? GetPlacement(Side squad) => _placements.TryGetValue(squad, out var place) ? place : (int?)null;

        public IReadOnlyList<SquadPlacement> GetPlacements()
        {
            var list = new List<SquadPlacement>(_placements.Count);
            foreach (var pair in _placements)
            {
                list.Add(new SquadPlacement(pair.Key, pair.Value));
            }

            list.Sort((a, b) => a.Place != b.Place ? a.Place.CompareTo(b.Place) : a.Squad.CompareTo(b.Squad));
            return list;
        }

        // 分隊にほかに立っている人がいなければ、起こせる人がもういないので、ダウン中の味方もいっしょに倒す。
        public BattleRoyaleHit ReportLethal(int victimId, int? attackerId)
        {
            RequirePlayer(victimId, nameof(victimId));
            if (attackerId.HasValue)
            {
                RequirePlayer(attackerId.Value, nameof(attackerId));
            }

            if (!IsLive || !IsStanding(victimId))
            {
                return BattleRoyaleHit.Ignored;
            }

            var squad = _mode.GetSide(victimId);
            if (_downed == null || !HasOtherStanding(squad, victimId))
            {
                ReportFall(attackerId, victimId);
                FinishDowned(squad, attackerId);
                return BattleRoyaleHit.Killed;
            }

            var byEnemy = attackerId.HasValue && attackerId.Value != victimId && _mode.GetSide(attackerId.Value) != squad;
            _downed.Down(victimId, attackerId ?? victimId, byEnemy);
            PlayerDowned?.Invoke(victimId, attackerId.HasValue && attackerId.Value != victimId ? attackerId : null);
            return BattleRoyaleHit.Downed;
        }

        public ReviveStep Revive(int actorId, int targetId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            RequirePlayer(actorId, nameof(actorId));
            RequirePlayer(targetId, nameof(targetId));

            if (_downed == null || !IsLive || actorId == targetId || !IsStanding(actorId) || !IsDowned(targetId)
                || _mode.GetSide(actorId) != _mode.GetSide(targetId))
            {
                return ReviveStep.Rejected;
            }

            var step = _downed.AddReviveProgress(targetId, actorId, deltaSeconds);
            if (step == ReviveStep.Completed)
            {
                _downed.Remove(targetId);
                PlayerRevived?.Invoke(actorId, targetId);
            }

            return step;
        }

        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (IsLive)
            {
                if (_downed != null)
                {
                    BleedOut(_downed.Tick(deltaSeconds));
                }

                FinishSquadsWithoutStanding();
                PlaceEliminatedSquads();
            }

            _mode.Tick(deltaSeconds);
        }

        public void RemovePlayer(int playerId)
        {
            _mode.RemovePlayer(playerId);
            _downed?.RemovePlayer(playerId);
        }

        private bool HasOtherStanding(Side squad, int exceptId)
        {
            foreach (var id in _mode.GetPlayerIds())
            {
                if (id != exceptId && _mode.GetSide(id) == squad && IsStanding(id))
                {
                    return true;
                }
            }

            return false;
        }

        private void FinishDowned(Side squad, int? fallbackCreditId)
        {
            if (_downed == null)
            {
                return;
            }

            foreach (var id in _downed.GetDownedIds())
            {
                if (!IsLive)
                {
                    return;
                }

                if (!_mode.HasPlayer(id) || _mode.GetSide(id) != squad || !_downed.IsDowned(id))
                {
                    continue;
                }

                var credit = _downed.GetBleedoutCreditId(id);
                if (!credit.HasValue && fallbackCreditId.HasValue && _mode.HasPlayer(fallbackCreditId.Value)
                    && _mode.GetSide(fallbackCreditId.Value) != squad)
                {
                    credit = fallbackCreditId;
                }

                _downed.Remove(id);
                ReportFall(credit, id);
                PlayerFinished?.Invoke(id, credit);
            }
        }

        private void FinishSquadsWithoutStanding()
        {
            if (_downed == null || _downed.Count == 0)
            {
                return;
            }

            var squads = new List<Side>();
            foreach (var id in _downed.GetDownedIds())
            {
                if (_mode.HasPlayer(id))
                {
                    var squad = _mode.GetSide(id);
                    if (!squads.Contains(squad) && !HasOtherStanding(squad, id))
                    {
                        squads.Add(squad);
                    }
                }
            }

            foreach (var squad in squads)
            {
                FinishDowned(squad, null);
            }
        }

        private void BleedOut(IReadOnlyList<int> bledOut)
        {
            foreach (var victimId in bledOut)
            {
                // 前の人のハンドラで試合が終わったり始め直されたりしたら、残りはもう数えない。
                if (!IsLive)
                {
                    return;
                }

                if (!_downed.IsDowned(victimId))
                {
                    continue;
                }

                var creditId = _downed.GetBleedoutCreditId(victimId);
                _downed.Remove(victimId);
                ReportFall(creditId, victimId);
                PlayerBledOut?.Invoke(victimId, creditId);
            }
        }

        private void ReportFall(int? killerId, int victimId)
        {
            if (killerId.HasValue && killerId.Value != victimId && _mode.HasPlayer(killerId.Value))
            {
                _mode.ReportKill(killerId.Value, victimId);
            }
            else
            {
                _mode.ReportDeath(victimId);
            }
        }

        private void PlaceEliminatedSquads()
        {
            List<Side> eliminated = null;
            foreach (var squad in _entrants)
            {
                if (!_placements.ContainsKey(squad) && IsOut(squad))
                {
                    (eliminated ??= new List<Side>()).Add(squad);
                }
            }

            if (eliminated == null)
            {
                return;
            }

            var place = SquadsRemaining - eliminated.Count + 1;
            foreach (var squad in eliminated)
            {
                _placements[squad] = place;
                CancelRedeploys(squad);
            }

            foreach (var squad in eliminated)
            {
                SquadEliminated?.Invoke(squad, place);
            }
        }

        private void CancelRedeploys(Side squad)
        {
            foreach (var id in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(id) == squad)
                {
                    _redeploy.Cancel(id);
                }
            }
        }

        // ダウンした味方しか残っていない分隊も、起こせる人がいないので全滅として扱う。
        private RedeployVerdict DecideRedeploy(int playerId)
        {
            var verdict = HasOtherStanding(_mode.GetSide(playerId), playerId)
                ? _redeploy.Begin(playerId)
                : RedeployVerdict.SquadWiped;
            RedeployDecided?.Invoke(playerId, verdict);
            return verdict;
        }

        private bool IsOut(Side squad) => _mode.CountMembers(squad) == 0 || _mode.IsSideEliminated(squad);

        private void OnModeEnded(Side? winner)
        {
            var remaining = new List<Side>();
            foreach (var squad in _entrants)
            {
                if (!_placements.ContainsKey(squad))
                {
                    remaining.Add(squad);
                }
            }

            // 勝った分隊を先頭に、残りは時間切れの時点で立っていた人・残っていた人の多い順に並べる。
            remaining.Sort((a, b) =>
            {
                if (winner.HasValue && (a == winner.Value) != (b == winner.Value))
                {
                    return a == winner.Value ? -1 : 1;
                }

                var byCount = FinalCount(b).CompareTo(FinalCount(a));
                return byCount != 0 ? byCount : a.CompareTo(b);
            });

            var place = 1;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (i > 0 && (IsWinner(remaining[i - 1], winner) || FinalCount(remaining[i - 1]) != FinalCount(remaining[i])))
                {
                    place = i + 1;
                }

                _placements[remaining[i]] = place;
            }

            _downed?.Clear();
            _finalCounts.Clear();
            Ended?.Invoke(winner);
        }

        private static bool IsWinner(Side squad, Side? winner) => winner.HasValue && squad == winner.Value;

        // ModeSession は Ended を呼ぶ前に全員の IsDown を戻すので、終わった後に数え直すと倒れた人まで残っていることになる。
        // そのため時間切れの判定 (終わる前) で数えた値を使う。勝ち残りで終わったときは残る分隊が 1 つ以下なので比べない。
        private long FinalCount(Side squad) => _finalCounts.TryGetValue(squad, out var count) ? count : 0L;

        private void RequirePlayer(int playerId, string paramName)
        {
            if (!_mode.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(paramName, playerId, "no player with this id");
            }
        }

        // 戻るかは分隊に立っている人がいるかで変わり、IndividualRespawn では見られないので、このセッションを見る口を ModeSession に渡す。
        private sealed class RedeployPolicy : IRespawnPolicy
        {
            private readonly BattleRoyaleSession _owner;

            public RedeployPolicy(BattleRoyaleSession owner)
            {
                _owner = owner;
            }

            public float? SecondsUntilNextWave => null;

            public bool WillReturn(ModeSession session, int playerId)
            {
                return _owner._redeploy.IsWaiting(playerId) && _owner.HasOtherStanding(session.GetSide(playerId), playerId);
            }

            public bool BeginWait(ModeSession session, int playerId)
            {
                return _owner.DecideRedeploy(playerId) == RedeployVerdict.Waiting && _owner._redeploy.Land(playerId);
            }

            public void Cancel(int playerId) => _owner._redeploy.Cancel(playerId);

            public void CancelAll() => _owner._redeploy.CancelAll();

            public void Reset() => _owner._redeploy.Reset();

            public void Tick(ModeSession session, float elapsedSeconds, Action<int> respawn)
            {
                if (session == null)
                {
                    throw new ArgumentNullException(nameof(session));
                }

                if (respawn == null)
                {
                    throw new ArgumentNullException(nameof(respawn));
                }

                foreach (var playerId in _owner._redeploy.Tick(elapsedSeconds))
                {
                    if (session.State != GameModeState.Playing)
                    {
                        return;
                    }

                    if (!_owner._redeploy.IsReady(playerId))
                    {
                        continue;
                    }

                    if (!session.HasPlayer(playerId) || !_owner.HasOtherStanding(session.GetSide(playerId), playerId))
                    {
                        _owner._redeploy.Cancel(playerId);
                        continue;
                    }

                    _owner._redeploy.Land(playerId);
                    respawn(playerId);
                }
            }

            public float? GetRemaining(int playerId) => _owner._redeploy.GetRemaining(playerId);
        }

        // LastSideStandingWin は誰もいない分隊を勝ちにしうるので使わない。
        private sealed class LastSquadStanding : IWinCondition
        {
            private readonly BattleRoyaleSession _owner;

            public LastSquadStanding(BattleRoyaleSession owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session)
            {
                var standingCount = 0;
                Side standing = default;
                foreach (var squad in _owner._entrants)
                {
                    if (!_owner._placements.ContainsKey(squad) && !_owner.IsOut(squad))
                    {
                        standingCount++;
                        standing = squad;
                    }
                }

                if (standingCount > 1)
                {
                    return ModeOutcome.Undecided;
                }

                return standingCount == 1 ? ModeOutcome.Win(standing) : ModeOutcome.Draw;
            }

            public ModeOutcome CheckTimeUp(ModeSession session) => ModeOutcome.Undecided;
        }

        private sealed class MostStandingAtTimeUp : IWinCondition
        {
            private readonly BattleRoyaleSession _owner;

            public MostStandingAtTimeUp(BattleRoyaleSession owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

            public ModeOutcome CheckTimeUp(ModeSession session)
            {
                return ModeOutcome.Decided(_owner.CaptureStandingAndPickLeader());
            }
        }

        // IsDowned / IsStanding に混ぜないのは、倒れきる処理が「立っている人」だけを対象にするため。時間切れの数え方だけ外のダウンを引く。
        private int CountExternallyDowned(Side squad)
        {
            if (ExternalDowned == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var id in _mode.GetPlayerIds())
            {
                if (_mode.GetSide(id) == squad && !_mode.IsDown(id) && !IsDowned(id) && ExternalDowned(id))
                {
                    count++;
                }
            }

            return count;
        }

        // 立っている人数を上の桁、残っている人数を下の桁にするのは、1 回の比較で立っている人数を先に、同じなら残っている人数で比べるため。
        private Side? CaptureStandingAndPickLeader()
        {
            _finalCounts.Clear();
            Side? leader = null;
            var best = -1L;
            var tied = false;
            foreach (var squad in _entrants)
            {
                if (_placements.ContainsKey(squad))
                {
                    continue;
                }

                var count = ((long)(CountStanding(squad) - CountExternallyDowned(squad)) << 32) | (uint)CountRemaining(squad);
                _finalCounts[squad] = count;
                if (count > best)
                {
                    leader = squad;
                    best = count;
                    tied = false;
                }
                else if (count == best)
                {
                    tied = true;
                }
            }

            return tied ? null : leader;
        }
    }
}

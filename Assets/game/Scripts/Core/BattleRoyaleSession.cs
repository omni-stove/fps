using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// バトルロイヤル。<see cref="Squads"/> で分隊を組み、倒れた人は戻らない (<see cref="NoRespawn"/>)。
    /// 体力が 0 になった人はダウンし、分隊の味方が起こすか出血で倒れるまでは試合から外れない。分隊の全員が倒れた分隊は脱落し、
    /// 脱落した順に順位 (<see cref="GetPlacement"/>) が付く。最後に残った 1 分隊 (<see cref="LastSideStandingWin"/>) が 1 位で勝つ。
    /// 時間は <see cref="Tick"/> で外から進める。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>体力が 0 になったことは <see cref="ReportLethal"/> で伝える。分隊にほかに立っている (倒れてもダウンしてもいない) 人がいればダウンさせ、
    /// いなければその場で倒し、ダウン中の分隊の味方もいっしょに倒す (起こせる人がもういないので)。
    /// <see cref="ActionRules.Downed"/> が Off のルールでは、ダウンさせずにその場で倒す。</item>
    /// <item>ダウン中は撃たれても倒れない (体の体力はもう 0)。倒れるのは出血か分隊の全滅のとき。どちらも、ダウンさせた敵 (いなければダウン中に最後に当てた敵)
    /// が倒したことにする (<see cref="DownedTracker.GetBleedoutCreditId"/>)。</item>
    /// <item>蘇生の長押しは <see cref="Revive"/> で、押している間 Tick ごとに 1 度、その秒数で進める。決まりは <see cref="DownedTracker"/> の蘇生と同じ。</item>
    /// <item>順位: 同じ Tick に脱落した分隊は同じ順位で、その Tick の頭で残っていた分隊の数から脱落した数を引いて 1 足した順位になる。
    /// 開始時に誰もいなかった分隊は数えない。全員が抜けた分隊も脱落と同じに扱う。試合が終わったときに残っていた分隊は、
    /// 立っている人・残っている人の多い順に 1 位から付ける (最後の 1 分隊なら 1 位、時間切れで同数なら同じ順位)。</item>
    /// <item>制限時間を <see cref="float.PositiveInfinity"/> にすると時間切れにならない (縮むエリアで決着させる)。
    /// 有限なら、時間切れで立っている人のいちばん多い分隊が勝ち、同じなら残っている人で比べ、それも同じなら引き分け。</item>
    /// </list>
    /// 参加と陣営分けは <see cref="Mode"/> に直接行う。ModeSession の Start・Reset・報告と抜けるのは、この型を通して呼ぶ。
    /// </remarks>
    public sealed class BattleRoyaleSession
    {
        public const int DefaultSquadCount = 4;
        public const int DefaultSquadSize = 2;

        /// <summary>ダウンさせたときに呼ばれる。引数はダウンした人と、ダウンさせた人 (分からないときと自滅では null)。</summary>
        public event Action<int, int?> PlayerDowned;

        /// <summary>味方を起こし終えたときに呼ばれる。引数は起こした人と起き上がった人。</summary>
        public event Action<int, int> PlayerRevived;

        /// <summary>出血で倒れたときに呼ばれる。引数は倒れた人と、倒したことにした敵 (いなければ null)。ModeSession に報告した後に呼ぶ。</summary>
        public event Action<int, int?> PlayerBledOut;

        /// <summary>
        /// 分隊の全滅で、ダウン中の人を倒したときに呼ばれる。引数は倒れた人と、倒したことにした敵 (いなければ null)。ModeSession に報告した後に呼ぶ。
        /// </summary>
        public event Action<int, int?> PlayerFinished;

        /// <summary>分隊が脱落して順位が決まったときに呼ばれる。引数は分隊と順位。勝敗を調べる前 (<see cref="Tick"/> の頭) に呼ぶ。</summary>
        public event Action<Side, int> SquadEliminated;

        /// <summary>試合が終わり、残っていた分隊にも順位を付け終えたときに呼ばれる。引数は勝った分隊で、引き分けなら null。</summary>
        public event Action<Side?> Ended;

        private readonly ModeSession _mode;

        // ダウンを使わないルールでは null。
        private readonly DownedTracker _downed;

        private readonly Dictionary<Side, int> _placements = new Dictionary<Side, int>();
        private readonly List<Side> _entrants = new List<Side>();

        // 時間切れの判定で数えた、分隊ごとの (立っている人数 << 32 | 残っている人数)。
        private readonly Dictionary<Side, long> _finalCounts = new Dictionary<Side, long>();

        /// <param name="squadCount">分隊の数。2 以上。</param>
        /// <param name="squadSize">1 隊の人数の上限。1 以上。</param>
        /// <param name="timeLimitSeconds">制限時間 (秒)。0 より大きい値。<see cref="float.PositiveInfinity"/> なら時間切れにならない。</param>
        /// <param name="actions">ダウン・出血・蘇生の決まり。</param>
        public BattleRoyaleSession(int squadCount, int squadSize, float timeLimitSeconds, ActionRules actions)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));
            if (actions.Downed == Downed.Revive)
            {
                // ダウン中は撃っても倒れないので、DownedTracker のダウン中の体力は使わない。1 以上でないと作れないので 1 を入れる。
                _downed = new DownedTracker(1, actions.DownedBleedoutSeconds, actions.ReviveSeconds);
            }

            _mode = new ModeSession(
                new Squads(squadCount, squadSize),
                timeLimitSeconds,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new LastSideStandingWin(), new MostStandingAtTimeUp(this) });
            _mode.Ended += OnModeEnded;
        }

        /// <summary>試合を表す ModeSession。参加者の出入りと、倒れたか (<see cref="ModeSession.IsDown"/>) はここで見る。</summary>
        public ModeSession Mode => _mode;

        public ActionRules Actions { get; }

        /// <summary>体力が 0 になった人をダウンさせるなら true (<see cref="ActionRules.Downed"/> が Revive)。</summary>
        public bool UsesDowned => _downed != null;

        /// <summary>試合中 (始まっていて終わっていない) なら true。</summary>
        public bool IsLive => _mode.State == GameModeState.Playing;

        /// <summary>開始時に 1 人以上いた分隊の数。始める前は 0。</summary>
        public int EntrantCount => _entrants.Count;

        /// <summary>開始時に 1 人以上いて、まだ順位の付いていない分隊の数。</summary>
        public int SquadsRemaining => _entrants.Count - _placements.Count;

        /// <summary>試合を始める。開始時に 1 人以上いる分隊を順位の対象にする。</summary>
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

        /// <summary>ダウンと順位を消し、<see cref="ModeSession.Reset"/> で最初に戻す。参加者と陣営分けは残す。</summary>
        public void Reset()
        {
            _downed?.Clear();
            _placements.Clear();
            _entrants.Clear();
            _finalCounts.Clear();
            _mode.Reset();
        }

        /// <summary>ダウン中なら true。</summary>
        public bool IsDowned(int playerId) => _downed != null && _downed.IsDowned(playerId);

        /// <summary>ダウン中の人の id を昇順で返す。</summary>
        public IReadOnlyList<int> GetDownedIds() => _downed != null ? _downed.GetDownedIds() : (IReadOnlyList<int>)Array.Empty<int>();

        /// <summary>出血で倒れるまでの残り秒数。ダウンしていなければ null。</summary>
        public float? GetBleedoutRemaining(int playerId) => _downed?.GetBleedoutRemaining(playerId);

        /// <summary>起こされている進み (0 以上 1 以下)。ダウンしていなければ 0。</summary>
        public float GetReviveProgress(int playerId) => _downed != null ? _downed.GetReviveProgress(playerId) : 0f;

        /// <summary>
        /// 最大体力が maxHealth の人が起こされた後の体力。最大体力に <see cref="ActionRules.DownedHealthFraction"/> を掛けて切り上げる。
        /// </summary>
        public int RevivedHealth(int maxHealth) => DownedTracker.HealthFor(maxHealth, Actions.DownedHealthFraction);

        /// <summary>playerId が倒れてもダウンしてもいなければ true。</summary>
        public bool IsStanding(int playerId) => !_mode.IsDown(playerId) && !IsDowned(playerId);

        /// <summary>squad のうち、まだ倒れていない人数 (ダウン中を含む)。</summary>
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

        /// <summary>squad のうち、ダウン中の人数。</summary>
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

        /// <summary>squad のうち、倒れてもダウンしてもいない人数。</summary>
        public int CountStanding(Side squad) => CountRemaining(squad) - CountDowned(squad);

        /// <summary>squad の順位。まだ付いていなければ null。</summary>
        public int? GetPlacement(Side squad) => _placements.TryGetValue(squad, out var place) ? place : (int?)null;

        /// <summary>付いた順位を、順位の昇順 (同じなら分隊の ID の昇順) で返す。</summary>
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

        /// <summary>
        /// victimId の体力が 0 になったことを伝える。試合中で、倒れてもダウンしてもいなければ、分隊にほかに立っている人がいればダウンさせ、
        /// いなければ倒して分隊のダウン中の人もいっしょに倒す。ダウンを使わないルールではその場で倒す。
        /// </summary>
        /// <param name="victimId">体力が 0 になった人。</param>
        /// <param name="attackerId">当てた人。分からないときは null。</param>
        /// <exception cref="ArgumentOutOfRangeException">victimId か attackerId が参加していない。</exception>
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

        /// <summary>
        /// actorId が、ダウンした分隊の味方 targetId を deltaSeconds だけ起こす。距離は呼ぶ側が確かめる。
        /// 試合中でない・同じ分隊でない・actorId が立っていない・targetId がダウンしていないときは <see cref="ReviveStep.Rejected"/>。
        /// </summary>
        /// <param name="actorId">起こす人。</param>
        /// <param name="targetId">ダウンした人。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">actorId か targetId が参加していないか、deltaSeconds が不正。</exception>
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

        /// <summary>
        /// 時間を deltaSeconds 進める。試合中なら、出血の残りを減らして 0 になった人を倒し、立っている人のいない分隊のダウン中の人を倒し、
        /// 脱落した分隊に順位を付けてから、<see cref="ModeSession.Tick"/> で勝敗を調べて時間を進める。
        /// </summary>
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

        /// <summary>playerId を抜く。ダウンからも外し、抜けた人には出血のキルを付けない。分隊の最後の 1 人なら、次の Tick で脱落として扱う。</summary>
        /// <exception cref="ArgumentOutOfRangeException">参加していない。</exception>
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

        /// <summary>squad のダウン中の人をすべて倒す。倒したことにする敵がいなければ fallbackCreditId (敵なら) にする。</summary>
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

        /// <summary>まだ順位の無い分隊のうち、脱落したか全員抜けた分隊に、同じ Tick の脱落として同じ順位を付ける。</summary>
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
            }

            foreach (var squad in eliminated)
            {
                SquadEliminated?.Invoke(squad, place);
            }
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

        /// <summary>時間切れのとき、立っている人のいちばん多い分隊の勝ち。同じなら残っている人で比べ、それも同じなら引き分け。</summary>
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

        /// <summary>
        /// まだ順位の無い分隊ごとに、立っている人数を上の桁・残っている人数を下の桁にした値を覚え、
        /// いちばん大きい分隊を返す。いちばん大きい分隊が複数あれば null (引き分け)。
        /// </summary>
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

                var count = ((long)CountStanding(squad) << 32) | (uint)CountRemaining(squad);
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

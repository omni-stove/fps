using System;
using System.Collections.Generic;

namespace Fps.Core
{
    /// <summary>
    /// エクスキューション。<see cref="RoundSession"/> を使う 2 チーム (<see cref="TwoTeams"/>) のラウンド制で、ラウンド中は戻らない (<see cref="NoRespawn"/>)。
    /// 体力が 0 になった人はダウンし、敵にとどめを刺されるか出血で倒れるまではラウンドから外れない。とどめを刺されていないダウンは味方が起こせる。
    /// 相手チームを全員倒した (<see cref="LastSideStandingWin"/>) チームがラウンドを取り、先に <see cref="RoundSession.WinsToWin"/> 本取ったチームの勝ち。
    /// 時間は <see cref="Tick"/> で外から進める。
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>体力が 0 になったことは <see cref="ReportLethal"/> で伝える。ダウン中は撃たれても倒れない (倒すにはとどめか出血)。
    /// <see cref="ActionRules.Downed"/> が Off のルールでは、ダウンさせずにその場で倒す。</item>
    /// <item>ダウンした人へのインタラクトの長押しは <see cref="Interact"/> で、押している間 Tick ごとに 1 度、その秒数で進める。
    /// 味方なら起こし (<see cref="ActionRules.ReviveSeconds"/>)、敵ならとどめを刺す (<see cref="ExecuteSeconds"/>)。
    /// 前の Tick から押されなかった累計は 0 に戻り、同じ相手を同じ Tick に押せるのは 1 人だけ (<see cref="DownedTracker"/> の蘇生と同じ決まり)。</item>
    /// <item>出血で倒れたときは、ダウンさせた敵 (いなければダウン中に最後に当てた敵) が倒したことにする (<see cref="DownedTracker.GetBleedoutCreditId"/>)。</item>
    /// <item>全員がダウンしただけではラウンドは終わらない。時間切れのときは、ダウンしていない人の多いチームがラウンドを取り、同じなら引き分け。</item>
    /// <item>ラウンドの待ちに入るたびにダウンととどめの累計を消す。体力と位置は Core の外にあるので、
    /// <see cref="RoundSession.PreparationStarted"/> を受けて戻す。</item>
    /// </list>
    /// 参加と陣営分けは <see cref="Mode"/> に直接行う。ModeSession と RoundSession の Start・Reset・報告は、この型を通して呼ぶ。
    /// </remarks>
    public sealed class ExecutionSession
    {
        public const int DefaultWinsToWin = 3;
        public const float DefaultPreparationSeconds = 3f;
        public const float DefaultRoundTimeLimitSeconds = 120f;

        /// <summary>とどめを刺すのに押し続ける秒数の既定値。蘇生 (<see cref="ActionRules.DefaultReviveSeconds"/>) より短くし、とどめを競り勝ちやすくする。</summary>
        public const float DefaultExecuteSeconds = 1.5f;

        /// <summary>ダウンさせたときに呼ばれる。引数はダウンした人と、ダウンさせた人 (分からないときと自滅では null)。</summary>
        public event Action<int, int?> PlayerDowned;

        /// <summary>味方を起こし終えたときに呼ばれる。引数は起こした人と起き上がった人。</summary>
        public event Action<int, int> PlayerRevived;

        /// <summary>とどめを刺したときに呼ばれる。引数はとどめを刺した人と倒れた人。ModeSession に倒したことを報告した後に呼ぶ。</summary>
        public event Action<int, int> PlayerExecuted;

        /// <summary>出血で倒れたときに呼ばれる。引数は倒れた人と、倒したことにした敵 (いなければ null)。ModeSession に報告した後に呼ぶ。</summary>
        public event Action<int, int?> PlayerBledOut;

        private readonly RoundSession _round;

        // ダウンを使わないルールでは null。
        private readonly DownedTracker _downed;

        // 倒れる人の id の昇順に回して、同じ入力なら同じ結果にする。
        private readonly SortedDictionary<int, ExecuteHold> _executeHolds = new SortedDictionary<int, ExecuteHold>();

        /// <param name="winsToWin">試合に勝つのに要るラウンドの勝ち数。1 以上。</param>
        /// <param name="preparationSeconds">各ラウンドの開始前の待ち (秒)。0 以上の有限値。</param>
        /// <param name="roundTimeLimitSeconds">1 ラウンドの制限時間 (秒)。0 より大きい値。</param>
        /// <param name="actions">ダウン・出血・蘇生の決まり。</param>
        /// <param name="executeSeconds">とどめを刺すのに押し続ける秒数。0 より大きい有限値。</param>
        public ExecutionSession(int winsToWin, float preparationSeconds, float roundTimeLimitSeconds, ActionRules actions, float executeSeconds)
        {
            Actions = actions ?? throw new ArgumentNullException(nameof(actions));

            if (!(executeSeconds > 0f) || float.IsInfinity(executeSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(executeSeconds), executeSeconds, "executeSeconds must be positive and finite");
            }

            ExecuteSeconds = executeSeconds;
            if (actions.Downed == Downed.Revive)
            {
                // ダウン中は撃っても倒れないので、DownedTracker のダウン中の体力は使わない。1 以上でないと作れないので 1 を入れる。
                _downed = new DownedTracker(1, actions.DownedBleedoutSeconds, actions.ReviveSeconds);
            }

            var mode = new ModeSession(
                new TwoTeams(),
                roundTimeLimitSeconds,
                Array.Empty<IScoreRule>(),
                new NoRespawn(),
                new IWinCondition[] { new LastSideStandingWin(), new MoreStandingAtTimeUp(this) });
            _round = new RoundSession(mode, winsToWin, preparationSeconds, 0);
            _round.PreparationStarted += OnPreparationStarted;
            _round.RoundEnded += OnRoundEnded;
        }

        /// <summary>ラウンドの進み。ラウンドの番号・勝ち数・待ちの残りはここで見る。イベントもここで購読する。</summary>
        public RoundSession Round => _round;

        /// <summary>今のラウンドを表す ModeSession。参加者の出入りと、倒れたか (<see cref="ModeSession.IsDown"/>) はここで見る。</summary>
        public ModeSession Mode => _round.Mode;

        public ActionRules Actions { get; }

        /// <summary>とどめを刺すのに押し続ける秒数。</summary>
        public float ExecuteSeconds { get; }

        /// <summary>体力が 0 になった人をダウンさせるなら true (<see cref="ActionRules.Downed"/> が Revive)。</summary>
        public bool UsesDowned => _downed != null;

        /// <summary>最後に終わったラウンドの勝者。引き分けか、まだどのラウンドも終わっていなければ null。</summary>
        public Side? LastRoundWinner { get; private set; }

        /// <summary>1 つでもラウンドが終わっていれば true。</summary>
        public bool HasFinishedRound { get; private set; }

        /// <summary>ラウンド中 (待ちでも終わった後でもない) なら true。</summary>
        public bool IsLive => _round.Phase == RoundPhase.Live && Mode.State == GameModeState.Playing;

        /// <summary>試合を始め、1 ラウンド目の待ちに入る。</summary>
        public void Start()
        {
            _round.Start();
        }

        /// <summary>ダウンととどめの累計を消し、<see cref="RoundSession.Reset"/> で最初に戻す。参加者と陣営分けは残す。</summary>
        public void Reset()
        {
            ClearDowned();
            LastRoundWinner = null;
            HasFinishedRound = false;
            _round.Reset();
        }

        /// <summary>ダウン中なら true。</summary>
        public bool IsDowned(int playerId) => _downed != null && _downed.IsDowned(playerId);

        /// <summary>ダウン中の人の id を昇順で返す。</summary>
        public IReadOnlyList<int> GetDownedIds() => _downed != null ? _downed.GetDownedIds() : (IReadOnlyList<int>)Array.Empty<int>();

        /// <summary>出血で倒れるまでの残り秒数。ダウンしていなければ null。</summary>
        public float? GetBleedoutRemaining(int playerId) => _downed?.GetBleedoutRemaining(playerId);

        /// <summary>起こされている進み (0 以上 1 以下)。ダウンしていなければ 0。</summary>
        public float GetReviveProgress(int playerId) => _downed != null ? _downed.GetReviveProgress(playerId) : 0f;

        /// <summary>とどめを刺されている進み (0 以上 1 以下)。誰も押していなければ 0。</summary>
        public float GetExecuteProgress(int playerId) =>
            _executeHolds.TryGetValue(playerId, out var hold) ? Math.Min(1f, hold.Seconds / ExecuteSeconds) : 0f;

        /// <summary>side のうち、このラウンドでまだ倒れていない人数 (ダウン中を含む)。</summary>
        public int CountRemaining(Side side)
        {
            var count = 0;
            foreach (var id in Mode.GetPlayerIds())
            {
                if (Mode.GetSide(id) == side && !Mode.IsDown(id))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>side のうち、ダウン中の人数。</summary>
        public int CountDowned(Side side)
        {
            var count = 0;
            foreach (var id in GetDownedIds())
            {
                if (Mode.HasPlayer(id) && Mode.GetSide(id) == side)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>side のうち、倒れてもダウンしてもいない人数。</summary>
        public int CountStanding(Side side) => CountRemaining(side) - CountDowned(side);

        /// <summary>
        /// 最大体力が maxHealth の人が起こされた後の体力。最大体力に <see cref="ActionRules.DownedHealthFraction"/> を掛けて切り上げる
        /// (<see cref="DownedTracker.HealthFor"/>)。
        /// </summary>
        public int RevivedHealth(int maxHealth) => DownedTracker.HealthFor(maxHealth, Actions.DownedHealthFraction);

        /// <summary>
        /// victimId の体力が 0 になったことを伝える。ラウンド中で、倒れてもダウンしてもいなければダウンさせる
        /// (ダウンを使わないルールではその場で倒す)。
        /// </summary>
        /// <param name="victimId">体力が 0 になった人。</param>
        /// <param name="attackerId">当てた人。分からないときは null。</param>
        /// <exception cref="ArgumentOutOfRangeException">victimId か attackerId が参加していない。</exception>
        public ExecutionHit ReportLethal(int victimId, int? attackerId)
        {
            RequirePlayer(victimId, nameof(victimId));
            if (attackerId.HasValue)
            {
                RequirePlayer(attackerId.Value, nameof(attackerId));
            }

            if (!IsLive || Mode.IsDown(victimId) || IsDowned(victimId))
            {
                return ExecutionHit.Ignored;
            }

            if (_downed == null)
            {
                ReportFall(attackerId, victimId);
                return ExecutionHit.Killed;
            }

            var byEnemy = attackerId.HasValue && attackerId.Value != victimId && Mode.GetSide(attackerId.Value) != Mode.GetSide(victimId);
            _downed.Down(victimId, attackerId ?? victimId, byEnemy);
            PlayerDowned?.Invoke(victimId, attackerId.HasValue && attackerId.Value != victimId ? attackerId : null);
            return ExecutionHit.Downed;
        }

        /// <summary>
        /// actorId が、ダウンした targetId へのインタラクトを deltaSeconds だけ押した。味方なら起こし、敵ならとどめを刺す累計を進める。
        /// 距離は呼ぶ側が確かめる。
        /// </summary>
        /// <param name="actorId">押した人。倒れてもダウンしてもいないこと。</param>
        /// <param name="targetId">ダウンした人。</param>
        /// <param name="deltaSeconds">進める秒数。0 以上の有限値。</param>
        /// <exception cref="ArgumentOutOfRangeException">actorId か targetId が参加していないか、deltaSeconds が不正。</exception>
        public ExecutionStep Interact(int actorId, int targetId, float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must not be negative and must be finite");
            }

            RequirePlayer(actorId, nameof(actorId));
            RequirePlayer(targetId, nameof(targetId));

            if (_downed == null || !IsLive || actorId == targetId || Mode.IsDown(actorId) || IsDowned(actorId) || !IsDowned(targetId))
            {
                return ExecutionStep.Rejected;
            }

            if (Mode.GetSide(actorId) == Mode.GetSide(targetId))
            {
                switch (_downed.AddReviveProgress(targetId, actorId, deltaSeconds))
                {
                    case ReviveStep.Progressed:
                        return ExecutionStep.Reviving;
                    case ReviveStep.Completed:
                        _downed.Remove(targetId);
                        _executeHolds.Remove(targetId);
                        PlayerRevived?.Invoke(actorId, targetId);
                        return ExecutionStep.Revived;
                    default:
                        return ExecutionStep.Rejected;
                }
            }

            if (_executeHolds.TryGetValue(targetId, out var hold))
            {
                if (hold.ExecutorId != actorId)
                {
                    if (hold.Touched)
                    {
                        return ExecutionStep.Rejected;
                    }

                    hold.ExecutorId = actorId;
                    hold.Seconds = 0f;
                }
            }
            else
            {
                hold = new ExecuteHold { ExecutorId = actorId };
                _executeHolds.Add(targetId, hold);
            }

            hold.Touched = true;
            hold.Seconds += deltaSeconds;
            if (hold.Seconds < ExecuteSeconds)
            {
                return ExecutionStep.Executing;
            }

            _downed.Remove(targetId);
            _executeHolds.Remove(targetId);
            _round.ReportKill(actorId, targetId);
            PlayerExecuted?.Invoke(actorId, targetId);
            return ExecutionStep.Executed;
        }

        /// <summary>
        /// 時間を deltaSeconds 進める。ラウンド中なら、前の Tick から押されなかった累計を 0 に戻し、出血の残りを減らして 0 になった人を倒してから、
        /// <see cref="RoundSession.Tick"/> でラウンドを進める (全員倒れたチームがあればここでラウンドが終わる)。
        /// </summary>
        public void Tick(float deltaSeconds)
        {
            if (!(deltaSeconds >= 0f) || float.IsPositiveInfinity(deltaSeconds))
            {
                throw new ArgumentOutOfRangeException(nameof(deltaSeconds), deltaSeconds, "deltaSeconds must be finite and not negative");
            }

            if (IsLive)
            {
                ForgetUntouchedExecutes();
                if (_downed != null)
                {
                    BleedOut(_downed.Tick(deltaSeconds));
                }
            }

            _round.Tick(deltaSeconds);
        }

        /// <summary>playerId を抜く。ダウンととどめの累計からも外し、抜けた人には出血のキルを付けない。</summary>
        /// <exception cref="ArgumentOutOfRangeException">参加していない。</exception>
        public void RemovePlayer(int playerId)
        {
            Mode.RemovePlayer(playerId);
            _downed?.RemovePlayer(playerId);

            List<int> removed = null;
            foreach (var pair in _executeHolds)
            {
                if (pair.Key == playerId || pair.Value.ExecutorId == playerId)
                {
                    (removed ??= new List<int>()).Add(pair.Key);
                }
            }

            if (removed != null)
            {
                foreach (var targetId in removed)
                {
                    _executeHolds.Remove(targetId);
                }
            }
        }

        private void BleedOut(IReadOnlyList<int> bledOut)
        {
            foreach (var victimId in bledOut)
            {
                // 前の人のハンドラでラウンドが終わったり始め直されたりしたら、残りはもう数えない。
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
                _executeHolds.Remove(victimId);
                ReportFall(creditId, victimId);
                PlayerBledOut?.Invoke(victimId, creditId);
            }
        }

        private void ReportFall(int? killerId, int victimId)
        {
            if (killerId.HasValue && killerId.Value != victimId)
            {
                _round.ReportKill(killerId.Value, victimId);
            }
            else
            {
                _round.ReportDeath(victimId);
            }
        }

        private void ForgetUntouchedExecutes()
        {
            List<int> untouched = null;
            foreach (var pair in _executeHolds)
            {
                if (pair.Value.Touched)
                {
                    pair.Value.Touched = false;
                }
                else
                {
                    (untouched ??= new List<int>()).Add(pair.Key);
                }
            }

            if (untouched != null)
            {
                foreach (var targetId in untouched)
                {
                    _executeHolds.Remove(targetId);
                }
            }
        }

        private void ClearDowned()
        {
            _downed?.Clear();
            _executeHolds.Clear();
        }

        private void OnPreparationStarted(int roundNumber)
        {
            ClearDowned();
        }

        private void OnRoundEnded(Side? winner)
        {
            LastRoundWinner = winner;
            HasFinishedRound = true;
        }

        private void RequirePlayer(int playerId, string paramName)
        {
            if (!Mode.HasPlayer(playerId))
            {
                throw new ArgumentOutOfRangeException(paramName, playerId, "no player with this id");
            }
        }

        private sealed class ExecuteHold
        {
            public int ExecutorId { get; set; }

            public float Seconds { get; set; }

            /// <summary>前の Tick からとどめを進めたら true。</summary>
            public bool Touched { get; set; }
        }

        /// <summary>時間切れのとき、倒れてもダウンしてもいない人の多いチームの勝ち。同じなら引き分け。</summary>
        private sealed class MoreStandingAtTimeUp : IWinCondition
        {
            private readonly ExecutionSession _owner;

            public MoreStandingAtTimeUp(ExecutionSession owner)
            {
                _owner = owner;
            }

            public double? ScoreLimit => null;

            public ModeOutcome CheckStanding(ModeSession session) => ModeOutcome.Undecided;

            public ModeOutcome CheckTimeUp(ModeSession session)
            {
                var alpha = _owner.CountStanding(Side.Alpha);
                var bravo = _owner.CountStanding(Side.Bravo);
                if (alpha == bravo)
                {
                    return ModeOutcome.Draw;
                }

                return ModeOutcome.Win(alpha > bravo ? Side.Alpha : Side.Bravo);
            }
        }
    }
}
